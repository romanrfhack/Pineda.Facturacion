using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

public sealed class SatProductServiceCatalogRepository : ISatProductServiceCatalogRepository
{
    private readonly BillingDbContext _dbContext;

    public SatProductServiceCatalogRepository(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<SatProductServiceCatalogEntry>> SearchAsync(string normalizedQuery, CancellationToken cancellationToken = default)
    {
        var codePrefix = $"{normalizedQuery}%";
        var textLike = $"%{normalizedQuery}%";

        return await _dbContext.SatProductServiceCatalogEntries
            .AsNoTracking()
            .Where(x => x.IsActive
                && (x.Code == normalizedQuery
                    || EF.Functions.Like(x.Code, codePrefix)
                    || EF.Functions.Like(x.NormalizedDescription, textLike)
                    || EF.Functions.Like(x.KeywordsNormalized, textLike)))
            .Take(100)
            .ToListAsync(cancellationToken);
    }
}
