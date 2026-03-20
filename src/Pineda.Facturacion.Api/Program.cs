using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Pineda.Facturacion.Application.Security;
using Pineda.Facturacion.Application.DependencyInjection;
using Pineda.Facturacion.Api.Endpoints;
using Pineda.Facturacion.Infrastructure.DependencyInjection;
using Pineda.Facturacion.Infrastructure.BillingWrite.DependencyInjection;
using Pineda.Facturacion.Infrastructure.FacturaloPlus.DependencyInjection;
using Pineda.Facturacion.Infrastructure.LegacyRead.DependencyInjection;
using Pineda.Facturacion.Infrastructure.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
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
app.MapAuthEndpoints();
app.MapAuditEventsEndpoints();
app.MapOrdersEndpoints();
app.MapSalesOrdersEndpoints();
app.MapBillingDocumentsEndpoints();
app.MapIssuerProfileEndpoints();
app.MapFiscalReceiversEndpoints();
app.MapProductFiscalProfilesEndpoints();
app.MapFiscalImportEndpoints();
app.MapFiscalDocumentsEndpoints();
app.MapAccountsReceivableEndpoints();
app.MapPaymentComplementsEndpoints();

app.Run();

public partial class Program;
