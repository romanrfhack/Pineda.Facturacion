using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Pineda.Facturacion.Application.Abstractions.Hashing;
using Pineda.Facturacion.Application.Abstractions.Importing;
using Pineda.Facturacion.Application.Abstractions.Security;
using Pineda.Facturacion.Infrastructure.Hashing;
using Pineda.Facturacion.Infrastructure.Excel;
using Pineda.Facturacion.Infrastructure.Options;
using Pineda.Facturacion.Infrastructure.Security;

namespace Pineda.Facturacion.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpContextAccessor();
        if (configuration is not null)
        {
            services.AddOptions<JwtAuthOptions>()
                .Bind(configuration.GetSection(JwtAuthOptions.SectionName))
                .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer), "JWT issuer is required.")
                .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "JWT audience is required.")
                .Validate(options => !string.IsNullOrWhiteSpace(options.SigningKey), "JWT signing key is required.")
                .Validate(options => options.ExpiresMinutes > 0, "JWT expiration must be greater than zero.");

            services.AddOptions<BootstrapAdminOptions>()
                .Bind(configuration.GetSection(BootstrapAdminOptions.SectionName));
        }

        services.AddSingleton<IContentHashGenerator, Sha256ContentHashGenerator>();
        services.AddSingleton<IExcelWorksheetReader, ClosedXmlWorksheetReader>();
        services.AddSingleton<IPasswordHasher, PasswordHasherService>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddHostedService<AuthBootstrapHostedService>();
        return services;
    }
}
