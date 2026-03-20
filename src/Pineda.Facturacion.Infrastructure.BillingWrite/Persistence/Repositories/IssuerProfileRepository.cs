using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

public class IssuerProfileRepository : IIssuerProfileRepository
{
    private readonly BillingDbContext _dbContext;

    public IssuerProfileRepository(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<IssuerProfile?> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.IssuerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IsActive, cancellationToken);
    }

    public Task<IssuerProfile?> GetByIdAsync(long issuerProfileId, CancellationToken cancellationToken = default)
    {
        return _dbContext.IssuerProfiles
            .FirstOrDefaultAsync(x => x.Id == issuerProfileId, cancellationToken);
    }

    public async Task AddAsync(IssuerProfile issuerProfile, CancellationToken cancellationToken = default)
    {
        await _dbContext.IssuerProfiles.AddAsync(issuerProfile, cancellationToken);
    }

    public Task UpdateAsync(IssuerProfile issuerProfile, CancellationToken cancellationToken = default)
    {
        _dbContext.IssuerProfiles.Update(issuerProfile);
        return Task.CompletedTask;
    }
}
