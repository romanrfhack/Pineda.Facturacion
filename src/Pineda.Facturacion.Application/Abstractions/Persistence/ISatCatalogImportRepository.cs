using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface ISatCatalogImportRepository
{
    Task<SatCatalogImport?> FindCompletedByChecksumAsync(
        string catalogType,
        string sourceChecksum,
        CancellationToken cancellationToken = default);

    Task AddAsync(SatCatalogImport satCatalogImport, CancellationToken cancellationToken = default);
}
