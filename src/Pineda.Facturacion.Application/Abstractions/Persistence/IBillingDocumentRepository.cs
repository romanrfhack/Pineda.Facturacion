using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface IBillingDocumentRepository
{
    Task<BillingDocument?> GetByIdAsync(long billingDocumentId, CancellationToken cancellationToken = default);

    Task<BillingDocument?> GetTrackedByIdAsync(long billingDocumentId, CancellationToken cancellationToken = default)
    {
        return GetByIdAsync(billingDocumentId, cancellationToken);
    }

    Task<BillingDocument?> GetBySalesOrderIdAsync(long salesOrderId, CancellationToken cancellationToken = default);

    Task AddAsync(BillingDocument billingDocument, CancellationToken cancellationToken = default);
}
