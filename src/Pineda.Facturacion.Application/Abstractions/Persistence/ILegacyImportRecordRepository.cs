using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface ILegacyImportRecordRepository
{
    Task<LegacyImportRecord?> GetByIdAsync(long legacyImportRecordId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<LegacyImportRecord?>(null);
    }

    Task<LegacyImportRecord?> GetBySourceDocumentAsync(
        string sourceSystem,
        string sourceTable,
        string sourceDocumentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LegacyImportRecord>> ListByBillingDocumentIdAsync(long billingDocumentId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<LegacyImportRecord>>([]);
    }

    Task AddAsync(LegacyImportRecord legacyImportRecord, CancellationToken cancellationToken = default);

    Task UpdateAsync(LegacyImportRecord legacyImportRecord, CancellationToken cancellationToken = default);
}
