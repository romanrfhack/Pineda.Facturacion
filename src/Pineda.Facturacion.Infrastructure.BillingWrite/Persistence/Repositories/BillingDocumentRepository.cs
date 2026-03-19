using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

public class BillingDocumentRepository : IBillingDocumentRepository
{
    private readonly BillingDbContext _dbContext;

    public BillingDocumentRepository(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<BillingDocument?> GetBySalesOrderIdAsync(long salesOrderId, CancellationToken cancellationToken = default)
    {
        return _dbContext.BillingDocuments
            .AsNoTracking()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.SalesOrderId == salesOrderId, cancellationToken);
    }

    public async Task AddAsync(BillingDocument billingDocument, CancellationToken cancellationToken = default)
    {
        await _dbContext.BillingDocuments.AddAsync(billingDocument, cancellationToken);
    }
}
