using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface IBillingDocumentItemRemovalRepository
{
    Task<IReadOnlyList<BillingDocumentItemRemoval>> ListByBillingDocumentIdAsync(long billingDocumentId, CancellationToken cancellationToken = default);

    Task AddAsync(BillingDocumentItemRemoval removal, CancellationToken cancellationToken = default);
}
