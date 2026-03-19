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
        services.AddScoped<ISalesOrderRepository, SalesOrderRepository>();
        services.AddScoped<ISalesOrderSnapshotRepository, SalesOrderRepository>();
        services.AddScoped<IBillingDocumentRepository, BillingDocumentRepository>();
        services.AddScoped<IUnitOfWork>(serviceProvider => serviceProvider.GetRequiredService<BillingDbContext>());

        return services;
    }
}
