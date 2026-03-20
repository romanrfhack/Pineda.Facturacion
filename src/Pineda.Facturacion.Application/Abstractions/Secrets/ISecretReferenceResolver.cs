namespace Pineda.Facturacion.Application.Abstractions.Secrets;

public interface ISecretReferenceResolver
{
    Task<string?> ResolveAsync(string referenceKey, CancellationToken cancellationToken = default);
}
