using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface IBillingDocumentItemRemovalRepository
{
    Task<IReadOnlyList<BillingDocumentItemRemoval>> ListByBillingDocumentIdAsync(long billingDocumentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BillingDocumentItemRemoval>> ListByIdsAsync(IReadOnlyCollection<long> removalIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BillingDocumentItemRemoval>> ListAvailablePendingBillingAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PendingBillingItemLookupModel>> ListAvailablePendingBillingLookupAsync(CancellationToken cancellationToken = default);

    Task AddAsync(BillingDocumentItemRemoval removal, CancellationToken cancellationToken = default);
}
