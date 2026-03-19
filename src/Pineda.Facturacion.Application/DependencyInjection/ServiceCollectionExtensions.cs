using Microsoft.Extensions.DependencyInjection;
using Pineda.Facturacion.Application.UseCases.CreateBillingDocument;
using Pineda.Facturacion.Application.UseCases.ImportLegacyOrder;

namespace Pineda.Facturacion.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ImportLegacyOrderService>();
        services.AddScoped<CreateBillingDocumentService>();
        return services;
    }
}
