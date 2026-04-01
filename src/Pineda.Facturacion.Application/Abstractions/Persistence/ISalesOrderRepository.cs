using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface ISalesOrderRepository
{
    Task<SalesOrder?> GetByLegacyImportRecordIdAsync(long legacyImportRecordId, CancellationToken cancellationToken = default);

    Task<SalesOrder?> GetTrackedByIdWithItemsAsync(long salesOrderId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<SalesOrder?>(null);
    }

    Task AddAsync(SalesOrder salesOrder, CancellationToken cancellationToken = default);
}
