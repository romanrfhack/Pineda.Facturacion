using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface ILegacyImportRevisionRepository
{
    Task<LegacyImportRevision?> GetCurrentByLegacyImportRecordIdAsync(long legacyImportRecordId, CancellationToken cancellationToken = default);

    Task<LegacyImportRevision?> GetTrackedCurrentByLegacyImportRecordIdAsync(long legacyImportRecordId, CancellationToken cancellationToken = default)
    {
        return GetCurrentByLegacyImportRecordIdAsync(legacyImportRecordId, cancellationToken);
    }

    Task<IReadOnlyList<LegacyImportRevision>> ListByLegacyImportRecordIdAsync(long legacyImportRecordId, CancellationToken cancellationToken = default);

    Task<int> GetNextRevisionNumberAsync(long legacyImportRecordId, CancellationToken cancellationToken = default);

    Task AddAsync(LegacyImportRevision revision, CancellationToken cancellationToken = default);
}
