using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface IProductFiscalProfileImportRepository
{
    Task AddBatchAsync(ProductFiscalProfileImportBatch batch, CancellationToken cancellationToken = default);

    Task<ProductFiscalProfileImportBatch?> GetBatchByIdAsync(long batchId, CancellationToken cancellationToken = default);

    Task<ProductFiscalProfileImportBatch?> GetBatchWithRowsForApplyAsync(long batchId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductFiscalProfileImportRow>> ListRowsByBatchIdAsync(long batchId, CancellationToken cancellationToken = default);
}
