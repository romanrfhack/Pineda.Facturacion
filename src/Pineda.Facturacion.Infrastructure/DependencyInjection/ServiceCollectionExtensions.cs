using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Pineda.Facturacion.Application.Abstractions.Communication;
using Pineda.Facturacion.Application.Abstractions.Documents;
using Pineda.Facturacion.Application.Abstractions.FiscalReceivers;
using Pineda.Facturacion.Application.Abstractions.Hashing;
using Pineda.Facturacion.Application.Abstractions.Importing;
using Pineda.Facturacion.Application.Abstractions.Reports;
using Pineda.Facturacion.Application.Abstractions.Security;
using Pineda.Facturacion.Application.Abstractions.Storage;
using Pineda.Facturacion.Infrastructure.Communication;
using Pineda.Facturacion.Infrastructure.Documents;
using Pineda.Facturacion.Infrastructure.Hashing;
using Pineda.Facturacion.Infrastructure.Excel;
using Pineda.Facturacion.Infrastructure.FiscalReceivers;
using Pineda.Facturacion.Infrastructure.Options;
using Pineda.Facturacion.Infrastructure.SatCatalogs;
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

            services.AddOptions<BootstrapSeedOptions>()
                .Bind(configuration.GetSection(BootstrapSeedOptions.SectionName));

            services.AddOptions<DevIdentitySeedOptions>()
                .Bind(configuration.GetSection(DevIdentitySeedOptions.SectionName));

            services.AddOptions<SmtpEmailOptions>()
                .Bind(configuration.GetSection(SmtpEmailOptions.SectionName));

            services.AddOptions<EmailDeliverySafetyOptions>()
                .Bind(configuration.GetSection(EmailDeliverySafetyOptions.SectionName));
            services.PostConfigure<EmailDeliverySafetyOptions>(options =>
            {
                options.SafeRecipient = FirstConfiguredValue(
                    options.SafeRecipient,
                    configuration["EMAIL_SAFE_RECIPIENT"]);
                options.ProductionBccRecipient = FirstConfiguredValue(
                    options.ProductionBccRecipient,
                    configuration["EMAIL_PRODUCTION_BCC"]);
            });

            services.AddOptions<IssuerLogoStorageOptions>()
                .Bind(configuration.GetSection(IssuerLogoStorageOptions.SectionName));
        }

        services.AddSingleton<IContentHashGenerator, Sha256ContentHashGenerator>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IFiscalReceiverSatCatalogProvider, FiscalReceiverSatCatalogProvider>();
        services.AddSingleton<SatProductServiceCatalogSeedSource>();
        services.AddSingleton<ISatCatalogDescriptionProvider, SatCatalogDescriptionProvider>();
        services.AddSingleton<IExcelWorksheetReader, ClosedXmlWorksheetReader>();
        services.AddSingleton<IStampedLegacyNotesReportExcelExporter, StampedLegacyNotesReportExcelExporter>();
        services.AddSingleton<ILoginAttemptThrottleService, InMemoryLoginAttemptThrottleService>();
        services.AddScoped<IFiscalDocumentPdfRenderer, FiscalDocumentPdfRenderer>();
        services.AddScoped<IPaymentComplementPdfRenderer, PaymentComplementPdfRenderer>();
        services.AddScoped<IReceivablesSummaryPdfRenderer, ReceivablesSummaryPdfRenderer>();
        services.AddSingleton<IIssuerProfileLogoStorage, IssuerProfileLogoStorage>();
        services.AddSingleton<EmailDeliverySafetyPolicy>();
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<IPasswordHasher, PasswordHasherService>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IdentityBootstrapService>();
        services.AddScoped<DevIdentitySeedService>();
        services.AddScoped<InitialProductionIdentitySeedService>();
        services.AddHostedService<DatabaseBootstrapHostedService>();
        services.AddHostedService<StandardVat16BackfillHostedService>();
        services.AddHostedService<AuthBootstrapHostedService>();
        services.AddHostedService<SatProductServiceCatalogBootstrapHostedService>();
        return services;
    }

    private static string FirstConfiguredValue(string? primaryValue, string? fallbackValue)
    {
        return !string.IsNullOrWhiteSpace(primaryValue)
            ? primaryValue
            : fallbackValue ?? string.Empty;
    }
}
