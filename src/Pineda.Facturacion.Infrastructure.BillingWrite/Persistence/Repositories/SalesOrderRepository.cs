using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

public class SalesOrderRepository : ISalesOrderRepository, ISalesOrderSnapshotRepository
{
    private readonly BillingDbContext _dbContext;

    public SalesOrderRepository(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<SalesOrder?> GetByLegacyImportRecordIdAsync(long legacyImportRecordId, CancellationToken cancellationToken = default)
    {
        return _dbContext.SalesOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.LegacyImportRecordId == legacyImportRecordId, cancellationToken);
    }

    public Task<SalesOrder?> GetByIdWithItemsAsync(long salesOrderId, CancellationToken cancellationToken = default)
    {
        return _dbContext.SalesOrders
            .AsNoTracking()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == salesOrderId, cancellationToken);
    }

    public Task<SalesOrder?> GetTrackedByIdWithItemsAsync(long salesOrderId, CancellationToken cancellationToken = default)
    {
        return _dbContext.SalesOrders
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == salesOrderId, cancellationToken);
    }

    public async Task<IReadOnlyList<SalesOrder>> GetByBillingDocumentIdWithItemsAsync(long billingDocumentId, CancellationToken cancellationToken = default)
    {
        return await (
            from salesOrder in _dbContext.SalesOrders.AsNoTracking()
            join legacyImportRecord in _dbContext.LegacyImportRecords.AsNoTracking()
                on salesOrder.LegacyImportRecordId equals legacyImportRecord.Id
            where legacyImportRecord.BillingDocumentId == billingDocumentId
            select salesOrder)
            .Include(x => x.Items)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(SalesOrder salesOrder, CancellationToken cancellationToken = default)
    {
        await _dbContext.SalesOrders.AddAsync(salesOrder, cancellationToken);
    }
}
