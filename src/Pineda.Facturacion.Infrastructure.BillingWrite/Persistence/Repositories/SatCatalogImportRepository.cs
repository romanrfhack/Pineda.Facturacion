using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

public sealed class SatCatalogImportRepository : ISatCatalogImportRepository
{
    private readonly BillingDbContext _dbContext;

    public SatCatalogImportRepository(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<SatCatalogImport?> FindCompletedAsync(
        string catalogType,
        string sourceVersion,
        string sourceFileName,
        string sourceChecksum,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.SatCatalogImports
            .AsNoTracking()
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(
                x => x.CatalogType == catalogType
                    && x.SourceVersion == sourceVersion
                    && x.SourceFileName == sourceFileName
                    && x.SourceChecksum == sourceChecksum
                    && x.Status == "completed",
                cancellationToken);
    }

    public async Task AddAsync(SatCatalogImport satCatalogImport, CancellationToken cancellationToken = default)
    {
        await _dbContext.SatCatalogImports.AddAsync(satCatalogImport, cancellationToken);
    }
}
