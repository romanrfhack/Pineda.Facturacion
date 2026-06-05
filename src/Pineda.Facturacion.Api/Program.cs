using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.FileProviders;
using Microsoft.OpenApi.Models;
using Pineda.Facturacion.Api.OperationalHardening;
using Pineda.Facturacion.Application.Security;
using Pineda.Facturacion.Application.DependencyInjection;
using Pineda.Facturacion.Api.Endpoints;
using Pineda.Facturacion.Api.Security;
using Pineda.Facturacion.Infrastructure.DependencyInjection;
using Pineda.Facturacion.Infrastructure.BillingWrite.DependencyInjection;
using Pineda.Facturacion.Infrastructure.BillingWrite.Operations.AccountsReceivable;
using Pineda.Facturacion.Infrastructure.BillingWrite.Operations.ProductFiscalProfiles;
using Pineda.Facturacion.Infrastructure.FacturaloPlus.DependencyInjection;
using Pineda.Facturacion.Infrastructure.LegacyRead.DependencyInjection;
using Pineda.Facturacion.Infrastructure.Options;
using Pineda.Facturacion.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Sandbox"))
{
    builder.Configuration.AddUserSecrets<Program>(optional: true);
}

var externalSecretReferencesPath = builder.Configuration["SecretReferences:ExternalJsonPath"];
if (!string.IsNullOrWhiteSpace(externalSecretReferencesPath))
{
    var externalSecretReferencesDirectory = Path.GetDirectoryName(externalSecretReferencesPath);
    var externalSecretReferencesFileName = Path.GetFileName(externalSecretReferencesPath);

    if (!string.IsNullOrWhiteSpace(externalSecretReferencesDirectory) &&
        !string.IsNullOrWhiteSpace(externalSecretReferencesFileName) &&
        Directory.Exists(externalSecretReferencesDirectory))
    {
        builder.Configuration.AddJsonFile(
            provider: new PhysicalFileProvider(externalSecretReferencesDirectory),
            path: externalSecretReferencesFileName,
            optional: true,
            reloadOnChange: false);
    }
}

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

        var correlationId = context.HttpContext.Request.Headers["X-Correlation-Id"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            context.ProblemDetails.Extensions["correlationId"] = correlationId;
        }
    };
});
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddCheck<BillingWriteDatabaseHealthCheck>("billing_write_db", tags: ["ready"]);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Pineda.Facturacion API",
        Version = "v1",
        Description = "Operational backend API for fiscal lifecycle, catalogs, accounts receivable, payment complements, and audit support."
    });

    options.CustomSchemaIds(type => (type.FullName ?? type.Name).Replace("+", ".", StringComparison.Ordinal));

    var bearerSecurityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Enter a JWT as: Bearer {token}",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = JwtBearerDefaults.AuthenticationScheme.ToLowerInvariant(),
        BearerFormat = "JWT",
        Reference = new OpenApiReference
        {
            Id = JwtBearerDefaults.AuthenticationScheme,
            Type = ReferenceType.SecurityScheme
        }
    };

    options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, bearerSecurityScheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [bearerSecurityScheme] = Array.Empty<string>()
    });
});
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddForwardedHeadersHardening(builder.Configuration);
builder.Services.AddLegacyReadInfrastructure(builder.Configuration);
builder.Services.AddBillingWriteInfrastructure(builder.Configuration);
builder.Services.AddFacturaloPlusInfrastructure(builder.Configuration);
var posCorsOrigins = ResolvePosCorsOrigins(builder.Configuration, builder.Environment);
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicyNames.PosClient, policy =>
    {
        if (posCorsOrigins.Length == 0)
        {
            return;
        }

        if (posCorsOrigins.Length == 1 && string.Equals(posCorsOrigins[0], "*", StringComparison.Ordinal))
        {
            policy.AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
            return;
        }

        policy.WithOrigins(posCorsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtOptions = builder.Configuration.GetSection(JwtAuthOptions.SectionName).Get<JwtAuthOptions>() ?? new JwtAuthOptions();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthorizationPolicyNames.Authenticated, policy => policy.RequireAuthenticatedUser());
    options.AddPolicy(AuthorizationPolicyNames.AdminOnly, policy => policy.RequireRole(AppRoleNames.Admin));
    options.AddPolicy(AuthorizationPolicyNames.SupervisorOrAdmin, policy => policy.RequireRole(AppRoleNames.Admin, AppRoleNames.FiscalSupervisor));
    options.AddPolicy(AuthorizationPolicyNames.OperatorOrAbove, policy => policy.RequireRole(AppRoleNames.Admin, AppRoleNames.FiscalSupervisor, AppRoleNames.FiscalOperator));
    options.AddPolicy(AuthorizationPolicyNames.AuditRead, policy => policy.RequireRole(AppRoleNames.Admin, AppRoleNames.FiscalSupervisor, AppRoleNames.Auditor));
    options.AddPolicy(AuthorizationPolicyNames.PosCreditRead, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context => HasPosCreditReadAccess(context.User));
    });
});

var app = builder.Build();
RuntimeSafetyValidator.ValidateOrThrow(app.Configuration, app.Environment);

if (args.Contains("seed-initial-production-users", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = app.Services.CreateAsyncScope();
    var seedService = scope.ServiceProvider.GetRequiredService<InitialProductionIdentitySeedService>();
    var result = await seedService.ExecuteAsync();

    Console.WriteLine("Initial production identity seed completed.");
    Console.WriteLine($"Created roles: {FormatList(result.CreatedRoles)}");
    Console.WriteLine($"Created users: {FormatList(result.CreatedUsers)}");
    Console.WriteLine($"Assigned Admin role: {FormatList(result.AssignedAdminRoleUsers)}");
    Console.WriteLine($"Skipped existing users: {FormatList(result.SkippedExistingUsers)}");
    return;
}

if (args.Contains(LegacyGenericSatResetCli.ResetCommandName, StringComparer.OrdinalIgnoreCase))
{
    try
    {
        await using var scope = app.Services.CreateAsyncScope();
        var command = LegacyGenericSatResetCli.ParseReset(args);
        var service = scope.ServiceProvider.GetRequiredService<ResetLegacyGenericSatAssignmentsService>();
        var result = await service.ExecuteAsync(command);
        LegacyGenericSatResetCli.WriteResetResult(result, app.Environment.EnvironmentName);
        Environment.ExitCode = result.IsSuccess ? 0 : 1;
        return;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine(exception.Message);
        Environment.ExitCode = 1;
        return;
    }
}

if (args.Contains(LegacyGenericSatResetCli.RollbackCommandName, StringComparer.OrdinalIgnoreCase))
{
    try
    {
        await using var scope = app.Services.CreateAsyncScope();
        var command = LegacyGenericSatResetCli.ParseRollback(args);
        var service = scope.ServiceProvider.GetRequiredService<RollbackLegacyGenericSatAssignmentsService>();
        var result = await service.ExecuteAsync(command);
        LegacyGenericSatResetCli.WriteRollbackResult(result, app.Environment.EnvironmentName);
        Environment.ExitCode = result.IsSuccess ? 0 : 1;
        return;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine(exception.Message);
        Environment.ExitCode = 1;
        return;
    }
}

if (args.Contains(MissingAccountsReceivableBackfillCli.CommandName, StringComparer.OrdinalIgnoreCase))
{
    try
    {
        await using var scope = app.Services.CreateAsyncScope();
        var command = MissingAccountsReceivableBackfillCli.Parse(args);
        var service = scope.ServiceProvider.GetRequiredService<BackfillMissingAccountsReceivableInvoicesService>();
        var result = await service.ExecuteAsync(command);
        MissingAccountsReceivableBackfillCli.WriteResult(result, app.Environment.EnvironmentName);
        Environment.ExitCode = result.IsSuccess ? 0 : 1;
        return;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine(exception.Message);
        Environment.ExitCode = 1;
        return;
    }
}

var enableSwagger = app.Configuration.GetValue<bool?>("OpenApi:EnableSwagger") ?? IsSwaggerEnabledByEnvironment(app.Environment);

if (app.Environment.IsEnvironment("Testing"))
{
    app.Use(async (context, next) =>
    {
        var rawRemoteIp = context.Request.Headers["X-Testing-Remote-Ip"].FirstOrDefault();
        if (IPAddress.TryParse(rawRemoteIp, out var remoteIpAddress))
        {
            context.Connection.RemoteIpAddress = remoteIpAddress;
        }

        await next();
    });
}

app.UseConfiguredForwardedHeaders();
app.UseExceptionHandler();

if (enableSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Pineda.Facturacion API v1");
        options.DocumentTitle = "Pineda.Facturacion API";
        options.DisplayRequestDuration();
        options.EnablePersistAuthorization();
    });
}

app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(correlationId))
    {
        correlationId = context.TraceIdentifier;
    }
    else
    {
        context.TraceIdentifier = correlationId;
    }

    context.Response.Headers["X-Correlation-Id"] = correlationId;
    await next();
});
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health/live", HealthCheckJsonResponseWriter.CreateOptions("live"));
app.MapHealthChecks("/health/ready", HealthCheckJsonResponseWriter.CreateOptions("ready"));

if (app.Environment.IsEnvironment("Testing"))
{
    app.MapGet("/_testing/throw/unhandled", static () =>
    {
        throw new InvalidOperationException("Simulated unhandled exception for integration testing.");
    }).AllowAnonymous().ExcludeFromDescription();

    app.MapGet("/_testing/throw/issuer-active-conflict", static () =>
    {
        throw new DbUpdateException(
            $"Duplicate entry '1' for key '{Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Configurations.IssuerProfileConfiguration.ActiveSingletonIndexName}'.");
    }).AllowAnonymous().ExcludeFromDescription();
}

app.MapAuthEndpoints();
app.MapAuditEventsEndpoints();
app.MapOrdersEndpoints();
app.MapOrderDebtSummaryEndpoints();
app.MapSalesOrdersEndpoints();
app.MapBillingDocumentsEndpoints();
app.MapIssuerProfileEndpoints();
app.MapFiscalReceiversEndpoints();
app.MapFiscalSatCatalogEndpoints();
app.MapProductFiscalProfilesEndpoints();
app.MapFiscalImportEndpoints();
app.MapFiscalDocumentsEndpoints();
app.MapAccountsReceivableEndpoints();
app.MapPosEndpoints();
app.MapPaymentComplementsEndpoints();
app.MapReportsEndpoints();

app.Run();

static bool IsSwaggerEnabledByEnvironment(IHostEnvironment environment)
{
    return environment.IsDevelopment()
        || environment.IsEnvironment("Local")
        || environment.IsEnvironment("Sandbox");
}

static string FormatList(IReadOnlyCollection<string> values)
{
    return values.Count == 0 ? "(none)" : string.Join(", ", values);
}

static string[] ResolvePosCorsOrigins(IConfiguration configuration, IHostEnvironment environment)
{
    var configuredOrigins = configuration
        .GetSection("Cors:Pos:Origins")
        .Get<string[]>()?
        .Where(origin => !string.IsNullOrWhiteSpace(origin))
        .Select(origin => origin.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray()
        ?? [];

    if (configuredOrigins.Any(origin => string.Equals(origin, "*", StringComparison.Ordinal)))
    {
        if (environment.IsProduction())
        {
            throw new InvalidOperationException("POS CORS origins cannot use '*' in Production.");
        }

        return ["*"];
    }

    if (configuredOrigins.Length > 0)
    {
        return configuredOrigins;
    }

    if (environment.IsDevelopment() || environment.IsEnvironment("Local") || environment.IsEnvironment("Sandbox") || environment.IsEnvironment("Testing"))
    {
        return
        [
            "http://localhost:3000",
            "http://127.0.0.1:3000",
            "http://localhost:4200",
            "http://127.0.0.1:4200",
            "http://localhost:5173",
            "http://127.0.0.1:5173"
        ];
    }

    return [];
}

static bool HasPosCreditReadAccess(ClaimsPrincipal user)
{
    return user.IsInRole(AppRoleNames.Admin)
        || user.IsInRole(AppRoleNames.FiscalSupervisor)
        || user.IsInRole(AppRoleNames.FiscalOperator)
        || HasPosCreditClaim(user, "scope")
        || HasPosCreditClaim(user, "permission");
}

static bool HasPosCreditClaim(ClaimsPrincipal user, string claimType)
{
    return user.FindAll(claimType)
        .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        .Any(value => string.Equals(value, AuthorizationPolicyNames.PosCreditReadPermission, StringComparison.OrdinalIgnoreCase));
}

public partial class Program;
