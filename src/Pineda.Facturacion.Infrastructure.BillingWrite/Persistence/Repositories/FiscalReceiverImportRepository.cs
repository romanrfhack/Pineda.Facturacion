using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

public class FiscalReceiverImportRepository : IFiscalReceiverImportRepository
{
    private readonly BillingDbContext _dbContext;

    public FiscalReceiverImportRepository(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddBatchAsync(FiscalReceiverImportBatch batch, CancellationToken cancellationToken = default)
    {
        await _dbContext.FiscalReceiverImportBatches.AddAsync(batch, cancellationToken);
    }

    public Task<FiscalReceiverImportBatch?> GetBatchByIdAsync(long batchId, CancellationToken cancellationToken = default)
    {
        return _dbContext.FiscalReceiverImportBatches
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == batchId, cancellationToken);
    }

    public Task<FiscalReceiverImportBatch?> GetBatchWithRowsForApplyAsync(long batchId, CancellationToken cancellationToken = default)
    {
        return _dbContext.FiscalReceiverImportBatches
            .Include(x => x.Rows.OrderBy(row => row.RowNumber))
            .FirstOrDefaultAsync(x => x.Id == batchId, cancellationToken);
    }

    public async Task<IReadOnlyList<FiscalReceiverImportRow>> ListRowsByBatchIdAsync(long batchId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.FiscalReceiverImportRows
            .AsNoTracking()
            .Where(x => x.BatchId == batchId)
            .OrderBy(x => x.RowNumber)
            .ToListAsync(cancellationToken);
    }
}
