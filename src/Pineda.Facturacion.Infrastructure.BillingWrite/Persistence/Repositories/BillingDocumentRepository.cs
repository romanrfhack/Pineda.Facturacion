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

    public Task<BillingDocument?> GetByIdAsync(long billingDocumentId, CancellationToken cancellationToken = default)
    {
        return _dbContext.BillingDocuments
            .AsNoTracking()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == billingDocumentId, cancellationToken);
    }

    public Task<BillingDocument?> GetTrackedByIdAsync(long billingDocumentId, CancellationToken cancellationToken = default)
    {
        return _dbContext.BillingDocuments
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == billingDocumentId, cancellationToken);
    }

    public Task<BillingDocument?> GetBySalesOrderIdAsync(long salesOrderId, CancellationToken cancellationToken = default)
    {
        return (
            from billingDocument in _dbContext.BillingDocuments.AsNoTracking().Include(x => x.Items)
            join legacyImportRecord in _dbContext.LegacyImportRecords.AsNoTracking()
                on billingDocument.Id equals legacyImportRecord.BillingDocumentId into legacyImportRecords
            from legacyImportRecord in legacyImportRecords.DefaultIfEmpty()
            where billingDocument.SalesOrderId == salesOrderId
                || (legacyImportRecord != null
                    && _dbContext.SalesOrders.Any(
                        salesOrder => salesOrder.Id == salesOrderId
                            && salesOrder.LegacyImportRecordId == legacyImportRecord.Id))
            select billingDocument)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(BillingDocument billingDocument, CancellationToken cancellationToken = default)
    {
        await _dbContext.BillingDocuments.AddAsync(billingDocument, cancellationToken);
    }
}
