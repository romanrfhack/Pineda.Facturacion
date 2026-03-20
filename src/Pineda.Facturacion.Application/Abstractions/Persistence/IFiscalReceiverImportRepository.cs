using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface IFiscalReceiverImportRepository
{
    Task AddBatchAsync(FiscalReceiverImportBatch batch, CancellationToken cancellationToken = default);

    Task<FiscalReceiverImportBatch?> GetBatchByIdAsync(long batchId, CancellationToken cancellationToken = default);

    Task<FiscalReceiverImportBatch?> GetBatchWithRowsForApplyAsync(long batchId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FiscalReceiverImportRow>> ListRowsByBatchIdAsync(long batchId, CancellationToken cancellationToken = default);
}
