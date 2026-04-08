using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface ISatProductServiceCatalogRepository
{
    Task<IReadOnlyList<SatProductServiceCatalogEntry>> SearchAsync(string normalizedQuery, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SatProductServiceCatalogEntry>> SearchAsync(
        string normalizedQuery,
        int maxCandidates,
        CancellationToken cancellationToken = default)
        => SearchAsync(normalizedQuery, cancellationToken);

    Task<SatProductServiceCatalogEntry?> GetByCodeAsync(string normalizedCode, CancellationToken cancellationToken = default)
        => Task.FromResult<SatProductServiceCatalogEntry?>(null);

    Task<SatCatalogSyncResult> SyncAsync(
        IReadOnlyList<SatProductServiceCatalogEntry> entries,
        string sourceVersion,
        DateTime syncedAtUtc,
        CancellationToken cancellationToken = default)
        => Task.FromException<SatCatalogSyncResult>(new NotSupportedException("Catalog synchronization is not supported by this repository."));
}
