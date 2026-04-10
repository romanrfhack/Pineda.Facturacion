using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using Pineda.Facturacion.Api.OperationalHardening;
using Pineda.Facturacion.Application.Security;
using Pineda.Facturacion.Application.DependencyInjection;
using Pineda.Facturacion.Api.Endpoints;
using Pineda.Facturacion.Infrastructure.DependencyInjection;
using Pineda.Facturacion.Infrastructure.BillingWrite.DependencyInjection;
using Pineda.Facturacion.Infrastructure.FacturaloPlus.DependencyInjection;
using Pineda.Facturacion.Infrastructure.LegacyRead.DependencyInjection;
using Pineda.Facturacion.Infrastructure.Options;
using Pineda.Facturacion.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Sandbox"))
{
    builder.Configuration.AddUserSecrets<Program>(optional: true);
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
builder.Services.AddLegacyReadInfrastructure(builder.Configuration);
builder.Services.AddBillingWriteInfrastructure(builder.Configuration);
builder.Services.AddFacturaloPlusInfrastructure(builder.Configuration);
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

var enableSwagger = app.Configuration.GetValue<bool?>("OpenApi:EnableSwagger") ?? IsSwaggerEnabledByEnvironment(app.Environment);

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
app.MapSalesOrdersEndpoints();
app.MapBillingDocumentsEndpoints();
app.MapIssuerProfileEndpoints();
app.MapFiscalReceiversEndpoints();
app.MapFiscalSatCatalogEndpoints();
app.MapProductFiscalProfilesEndpoints();
app.MapFiscalImportEndpoints();
app.MapFiscalDocumentsEndpoints();
app.MapAccountsReceivableEndpoints();
app.MapPaymentComplementsEndpoints();

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

public partial class Program;
