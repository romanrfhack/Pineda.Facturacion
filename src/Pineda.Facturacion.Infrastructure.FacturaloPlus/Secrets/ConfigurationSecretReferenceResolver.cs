using Microsoft.Extensions.Options;
using Pineda.Facturacion.Application.Abstractions.Secrets;
using Pineda.Facturacion.Infrastructure.FacturaloPlus.Options;

namespace Pineda.Facturacion.Infrastructure.FacturaloPlus.Secrets;

public class ConfigurationSecretReferenceResolver : ISecretReferenceResolver
{
    private readonly SecretReferenceOptions _options;

    public ConfigurationSecretReferenceResolver(IOptions<SecretReferenceOptions> options)
    {
        _options = options.Value;
    }

    public Task<string?> ResolveAsync(string referenceKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(referenceKey))
        {
            return Task.FromResult<string?>(null);
        }

        return Task.FromResult(
            _options.Values.TryGetValue(referenceKey, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : null);
    }
}
