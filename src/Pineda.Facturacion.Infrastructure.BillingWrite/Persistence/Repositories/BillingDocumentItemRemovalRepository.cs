using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

public sealed class BillingDocumentItemRemovalRepository : IBillingDocumentItemRemovalRepository
{
    private readonly BillingDbContext _dbContext;

    public BillingDocumentItemRemovalRepository(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<BillingDocumentItemRemoval>> ListByBillingDocumentIdAsync(long billingDocumentId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.BillingDocumentItemRemovals
            .Where(x => x.BillingDocumentId == billingDocumentId)
            .OrderBy(x => x.RemovedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(BillingDocumentItemRemoval removal, CancellationToken cancellationToken = default)
    {
        await _dbContext.BillingDocumentItemRemovals.AddAsync(removal, cancellationToken);
    }
}
