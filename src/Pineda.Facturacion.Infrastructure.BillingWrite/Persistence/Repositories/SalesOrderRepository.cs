using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

public class SalesOrderRepository : ISalesOrderRepository
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

    public async Task AddAsync(SalesOrder salesOrder, CancellationToken cancellationToken = default)
    {
        await _dbContext.SalesOrders.AddAsync(salesOrder, cancellationToken);
    }
}
