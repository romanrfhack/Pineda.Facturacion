using Microsoft.Extensions.DependencyInjection;
using Pineda.Facturacion.Application.Abstractions.Hashing;
using Pineda.Facturacion.Infrastructure.Hashing;

namespace Pineda.Facturacion.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IContentHashGenerator, Sha256ContentHashGenerator>();
        return services;
    }
}
