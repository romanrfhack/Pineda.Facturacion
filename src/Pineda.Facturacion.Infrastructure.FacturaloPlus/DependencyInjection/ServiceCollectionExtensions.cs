using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pineda.Facturacion.Application.Abstractions.Pac;
using Pineda.Facturacion.Application.Abstractions.Secrets;
using Pineda.Facturacion.Infrastructure.FacturaloPlus.FacturaloPlus;
using Pineda.Facturacion.Infrastructure.FacturaloPlus.Options;
using Pineda.Facturacion.Infrastructure.FacturaloPlus.Secrets;

namespace Pineda.Facturacion.Infrastructure.FacturaloPlus.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFacturaloPlusInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string facturaloPlusSectionName = FacturaloPlusOptions.SectionName,
        string secretReferencesSectionName = SecretReferenceOptions.SectionName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<FacturaloPlusOptions>()
            .Bind(configuration.GetSection(facturaloPlusSectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.BaseUrl), "FacturaloPlus base URL is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.StampPath), "FacturaloPlus stamp path is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.PaymentComplementStampPath), "FacturaloPlus payment complement stamp path is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.PaymentComplementCancelPath), "FacturaloPlus payment complement cancel path is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.PaymentComplementStatusQueryPath), "FacturaloPlus payment complement status query path is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.CancelPath), "FacturaloPlus cancel path is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.StatusQueryPath), "FacturaloPlus status query path is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.RemoteCfdiQueryPath), "FacturaloPlus remote CFDI query path is required.");

        services.AddOptions<SecretReferenceOptions>()
            .Bind(configuration.GetSection(secretReferencesSectionName));

        services.AddSingleton<ISecretReferenceResolver, ConfigurationSecretReferenceResolver>();

        services.AddHttpClient<IFiscalStampingGateway, FacturaloPlusStampingGateway>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<FacturaloPlusOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds <= 0 ? 30 : options.TimeoutSeconds);
        });

        services.AddHttpClient<IFiscalCancellationGateway, FacturaloPlusCancellationGateway>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<FacturaloPlusOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds <= 0 ? 30 : options.TimeoutSeconds);
        });

        services.AddHttpClient<IPaymentComplementStampingGateway, FacturaloPlusPaymentComplementStampingGateway>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<FacturaloPlusOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds <= 0 ? 30 : options.TimeoutSeconds);
        });

        services.AddHttpClient<IPaymentComplementCancellationGateway, FacturaloPlusPaymentComplementCancellationGateway>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<FacturaloPlusOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds <= 0 ? 30 : options.TimeoutSeconds);
        });

        services.AddHttpClient<IPaymentComplementStatusQueryGateway, FacturaloPlusPaymentComplementStatusQueryGateway>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<FacturaloPlusOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds <= 0 ? 30 : options.TimeoutSeconds);
        });

        services.AddHttpClient<IFiscalStatusQueryGateway, FacturaloPlusStatusQueryGateway>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<FacturaloPlusOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds <= 0 ? 30 : options.TimeoutSeconds);
        });

        return services;
    }
}
