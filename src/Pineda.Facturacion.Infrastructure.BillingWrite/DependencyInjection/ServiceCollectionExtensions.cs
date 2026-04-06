using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Infrastructure.BillingWrite.Options;
using Pineda.Facturacion.Infrastructure.BillingWrite.Persistence;
using Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBillingWriteInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = BillingWriteOptions.SectionName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<BillingWriteOptions>()
            .Bind(configuration.GetSection(sectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), "BillingWrite connection string is required.");

        services.AddDbContext<BillingDbContext>((serviceProvider, options) =>
        {
            var billingWriteOptions = serviceProvider.GetRequiredService<IOptions<BillingWriteOptions>>().Value;
            options.UseMySql(
                billingWriteOptions.ConnectionString,
                ServerVersion.AutoDetect(billingWriteOptions.ConnectionString));
        });

        services.AddScoped<ILegacyImportRecordRepository, LegacyImportRecordRepository>();
        services.AddScoped<ILegacyImportRevisionRepository, LegacyImportRevisionRepository>();
        services.AddScoped<ISalesOrderRepository, SalesOrderRepository>();
        services.AddScoped<ISalesOrderSnapshotRepository, SalesOrderRepository>();
        services.AddScoped<IAppUserRepository, AppUserRepository>();
        services.AddScoped<IAppRoleRepository, AppRoleRepository>();
        services.AddScoped<IAppUserRoleRepository, AppUserRoleRepository>();
        services.AddScoped<IAuditEventRepository, AuditEventRepository>();
        services.AddScoped<IBillingDocumentRepository, BillingDocumentRepository>();
        services.AddScoped<IBillingDocumentItemRemovalRepository, BillingDocumentItemRemovalRepository>();
        services.AddScoped<IBillingDocumentPendingItemAssignmentRepository, BillingDocumentPendingItemAssignmentRepository>();
        services.AddScoped<IBillingDocumentLookupRepository, BillingDocumentLookupRepository>();
        services.AddScoped<IImportedLegacyOrderLookupRepository, ImportedLegacyOrderLookupRepository>();
        services.AddScoped<IFiscalDocumentRepository, FiscalDocumentRepository>();
        services.AddScoped<IFiscalStampRepository, FiscalStampRepository>();
        services.AddScoped<IFiscalCancellationRepository, FiscalCancellationRepository>();
        services.AddScoped<IAccountsReceivableInvoiceRepository, AccountsReceivableInvoiceRepository>();
        services.AddScoped<IAccountsReceivablePaymentRepository, AccountsReceivablePaymentRepository>();
        services.AddScoped<IAccountsReceivablePaymentApplicationRepository, AccountsReceivablePaymentApplicationRepository>();
        services.AddScoped<IAccountsReceivableCollectionRepository, AccountsReceivableCollectionRepository>();
        services.AddScoped<IPaymentComplementDocumentRepository, PaymentComplementDocumentRepository>();
        services.AddScoped<IPaymentComplementStampRepository, PaymentComplementStampRepository>();
        services.AddScoped<IPaymentComplementCancellationRepository, PaymentComplementCancellationRepository>();
        services.AddScoped<IRepBaseDocumentRepository, RepBaseDocumentRepository>();
        services.AddScoped<IInternalRepBaseDocumentStateRepository, InternalRepBaseDocumentStateRepository>();
        services.AddScoped<IExternalRepBaseDocumentRepository, ExternalRepBaseDocumentRepository>();
        services.AddScoped<IIssuerProfileRepository, IssuerProfileRepository>();
        services.AddScoped<IFiscalReceiverRepository, FiscalReceiverRepository>();
        services.AddScoped<IProductFiscalProfileRepository, ProductFiscalProfileRepository>();
        services.AddScoped<ISatProductServiceCatalogRepository, SatProductServiceCatalogRepository>();
        services.AddScoped<IFiscalReceiverImportRepository, FiscalReceiverImportRepository>();
        services.AddScoped<IProductFiscalProfileImportRepository, ProductFiscalProfileImportRepository>();
        services.AddScoped<IUnitOfWork>(serviceProvider => serviceProvider.GetRequiredService<BillingDbContext>());

        return services;
    }
}
