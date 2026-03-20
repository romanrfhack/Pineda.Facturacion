using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

public class ProductFiscalProfileImportRepository : IProductFiscalProfileImportRepository
{
    private readonly BillingDbContext _dbContext;

    public ProductFiscalProfileImportRepository(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddBatchAsync(ProductFiscalProfileImportBatch batch, CancellationToken cancellationToken = default)
    {
        await _dbContext.ProductFiscalProfileImportBatches.AddAsync(batch, cancellationToken);
    }

    public Task<ProductFiscalProfileImportBatch?> GetBatchByIdAsync(long batchId, CancellationToken cancellationToken = default)
    {
        return _dbContext.ProductFiscalProfileImportBatches
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == batchId, cancellationToken);
    }

    public Task<ProductFiscalProfileImportBatch?> GetBatchWithRowsForApplyAsync(long batchId, CancellationToken cancellationToken = default)
    {
        return _dbContext.ProductFiscalProfileImportBatches
            .Include(x => x.Rows.OrderBy(row => row.RowNumber))
            .FirstOrDefaultAsync(x => x.Id == batchId, cancellationToken);
    }

    public async Task<IReadOnlyList<ProductFiscalProfileImportRow>> ListRowsByBatchIdAsync(long batchId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ProductFiscalProfileImportRows
            .AsNoTracking()
            .Where(x => x.BatchId == batchId)
            .OrderBy(x => x.RowNumber)
            .ToListAsync(cancellationToken);
    }
}
