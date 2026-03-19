using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pineda.Facturacion.Application.Abstractions.Legacy;
using Pineda.Facturacion.Infrastructure.LegacyRead.Options;
using Pineda.Facturacion.Infrastructure.LegacyRead.Readers;

namespace Pineda.Facturacion.Infrastructure.LegacyRead.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLegacyReadInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = LegacyReadOptions.SectionName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<LegacyReadOptions>()
            .Bind(configuration.GetSection(sectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), "LegacyRead connection string is required.");

        services.AddScoped<ILegacyOrderReader>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<LegacyReadOptions>>().Value;
            return new LegacyOrderReader(options);
        });

        return services;
    }
}
