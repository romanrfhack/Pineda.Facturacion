using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface ISalesOrderSnapshotRepository
{
    Task<SalesOrder?> GetByIdWithItemsAsync(long salesOrderId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SalesOrder>> GetByBillingDocumentIdWithItemsAsync(long billingDocumentId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<SalesOrder>>([]);
    }
}
