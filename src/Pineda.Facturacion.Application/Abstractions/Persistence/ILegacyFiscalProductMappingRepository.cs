using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface ILegacyFiscalProductMappingRepository
{
    Task<FiscalProductMappingImportBatch?> FindBatchByChecksumAsync(
        string sourceChecksum,
        CancellationToken cancellationToken = default);

    Task AddBatchAsync(
        FiscalProductMappingImportBatch batch,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FiscalProductMappingImportBatch>> ListRecentBatchesAsync(
        int maxResults,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LegacyFiscalProductMapping>> FindActiveExactCandidatesAsync(
        string? normalizedInternalCode,
        string? normalizedDescription,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LegacyFiscalProductMapping>> FindActiveDescriptionCandidatesAsync(
        string normalizedDescription,
        int maxCandidates,
        CancellationToken cancellationToken = default);
}
