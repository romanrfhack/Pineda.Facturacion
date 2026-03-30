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

    public async Task<IReadOnlyList<BillingDocumentItemRemoval>> ListByIdsAsync(IReadOnlyCollection<long> removalIds, CancellationToken cancellationToken = default)
    {
        if (removalIds.Count == 0)
        {
            return [];
        }

        return await _dbContext.BillingDocumentItemRemovals
            .Where(x => removalIds.Contains(x.Id))
            .OrderBy(x => x.RemovedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BillingDocumentItemRemoval>> ListAvailablePendingBillingAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.BillingDocumentItemRemovals
            .Where(x => x.RemovalDisposition == Domain.Enums.BillingDocumentItemRemovalDisposition.PendingBilling && x.AvailableForPendingBillingReuse)
            .Where(x => !_dbContext.BillingDocumentPendingItemAssignments.Any(a => a.BillingDocumentItemRemovalId == x.Id && a.ReleasedAtUtc == null))
            .OrderByDescending(x => x.RemovedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PendingBillingItemLookupModel>> ListAvailablePendingBillingLookupAsync(CancellationToken cancellationToken = default)
    {
        return await (
                from removal in _dbContext.BillingDocumentItemRemovals.AsNoTracking()
                join salesOrder in _dbContext.SalesOrders.AsNoTracking() on removal.SalesOrderId equals salesOrder.Id
                where removal.RemovalDisposition == Domain.Enums.BillingDocumentItemRemovalDisposition.PendingBilling
                    && removal.AvailableForPendingBillingReuse
                where !_dbContext.BillingDocumentPendingItemAssignments.Any(a => a.BillingDocumentItemRemovalId == removal.Id && a.ReleasedAtUtc == null)
                orderby removal.RemovedAtUtc descending, removal.Id descending
                select new PendingBillingItemLookupModel
                {
                    RemovalId = removal.Id,
                    BillingDocumentId = removal.BillingDocumentId,
                    FiscalDocumentId = removal.FiscalDocumentId,
                    SalesOrderId = removal.SalesOrderId,
                    SalesOrderItemId = removal.SalesOrderItemId,
                    SourceLegacyOrderId = removal.SourceLegacyOrderId,
                    CustomerName = salesOrder.CustomerName,
                    SourceSalesOrderLineNumber = removal.SourceSalesOrderLineNumber,
                    ProductInternalCode = removal.ProductInternalCode,
                    Description = removal.Description,
                    QuantityRemoved = removal.QuantityRemoved,
                    RemovalReason = removal.RemovalReason.ToString(),
                    Observations = removal.Observations,
                    RemovalDisposition = removal.RemovalDisposition.ToString(),
                    RemovedAtUtc = removal.RemovedAtUtc
                })
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(BillingDocumentItemRemoval removal, CancellationToken cancellationToken = default)
    {
        await _dbContext.BillingDocumentItemRemovals.AddAsync(removal, cancellationToken);
    }
}
