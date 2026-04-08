using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface ISatClaveUnidadRepository
{
    Task<IReadOnlyList<SatClaveUnidad>> SearchAsync(
        string normalizedQuery,
        int maxCandidates,
        CancellationToken cancellationToken = default);

    Task<SatClaveUnidad?> GetByCodeAsync(string normalizedCode, CancellationToken cancellationToken = default);

    Task<SatCatalogSyncResult> SyncAsync(
        IReadOnlyList<SatClaveUnidad> entries,
        string sourceVersion,
        DateTime syncedAtUtc,
        CancellationToken cancellationToken = default);
}
