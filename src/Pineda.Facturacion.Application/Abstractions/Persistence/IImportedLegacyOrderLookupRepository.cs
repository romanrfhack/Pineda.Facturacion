namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface IImportedLegacyOrderLookupRepository
{
    Task<IReadOnlyDictionary<string, ImportedLegacyOrderLookupModel>> GetByLegacyOrderIdsAsync(
        IReadOnlyCollection<string> legacyOrderIds,
        CancellationToken cancellationToken = default);
}
