using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface ILegacyImportRecordRepository
{
    Task<LegacyImportRecord?> GetBySourceDocumentAsync(
        string sourceSystem,
        string sourceTable,
        string sourceDocumentId,
        CancellationToken cancellationToken = default);

    Task AddAsync(LegacyImportRecord legacyImportRecord, CancellationToken cancellationToken = default);

    Task UpdateAsync(LegacyImportRecord legacyImportRecord, CancellationToken cancellationToken = default);
}
