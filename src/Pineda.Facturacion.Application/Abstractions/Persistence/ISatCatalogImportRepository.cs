using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface ISatCatalogImportRepository
{
    Task<SatCatalogImport?> FindCompletedAsync(
        string catalogType,
        string sourceVersion,
        string sourceFileName,
        string sourceChecksum,
        CancellationToken cancellationToken = default);

    Task AddAsync(SatCatalogImport satCatalogImport, CancellationToken cancellationToken = default);
}
