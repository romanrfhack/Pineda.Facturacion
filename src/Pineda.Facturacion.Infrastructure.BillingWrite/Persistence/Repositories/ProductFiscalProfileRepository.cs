using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

public class ProductFiscalProfileRepository : IProductFiscalProfileRepository
{
    private readonly BillingDbContext _dbContext;

    public ProductFiscalProfileRepository(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<ProductFiscalProfile>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var prefix = $"{query}%";

        return await _dbContext.ProductFiscalProfiles
            .AsNoTracking()
            .Where(x => EF.Functions.Like(x.InternalCode, prefix) || EF.Functions.Like(x.NormalizedDescription, prefix))
            .Take(20)
            .ToListAsync(cancellationToken);
    }

    public Task<ProductFiscalProfile?> GetByInternalCodeAsync(string normalizedInternalCode, CancellationToken cancellationToken = default)
    {
        return _dbContext.ProductFiscalProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.InternalCode == normalizedInternalCode, cancellationToken);
    }

    public Task<ProductFiscalProfile?> GetByIdAsync(long productFiscalProfileId, CancellationToken cancellationToken = default)
    {
        return _dbContext.ProductFiscalProfiles
            .FirstOrDefaultAsync(x => x.Id == productFiscalProfileId, cancellationToken);
    }

    public async Task AddAsync(ProductFiscalProfile productFiscalProfile, CancellationToken cancellationToken = default)
    {
        await _dbContext.ProductFiscalProfiles.AddAsync(productFiscalProfile, cancellationToken);
    }

    public Task UpdateAsync(ProductFiscalProfile productFiscalProfile, CancellationToken cancellationToken = default)
    {
        _dbContext.ProductFiscalProfiles.Update(productFiscalProfile);
        return Task.CompletedTask;
    }
}
