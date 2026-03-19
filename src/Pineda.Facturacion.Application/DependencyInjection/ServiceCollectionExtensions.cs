using Microsoft.Extensions.DependencyInjection;
using Pineda.Facturacion.Application.UseCases.ImportLegacyOrder;

namespace Pineda.Facturacion.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ImportLegacyOrderService>();
        return services;
    }
}
