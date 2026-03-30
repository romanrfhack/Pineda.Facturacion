using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface IBillingDocumentPendingItemAssignmentRepository
{
    Task<IReadOnlyList<BillingDocumentPendingItemAssignment>> ListActiveByBillingDocumentIdAsync(long billingDocumentId, CancellationToken cancellationToken = default);

    Task<BillingDocumentPendingItemAssignment?> GetActiveByRemovalIdAsync(long billingDocumentItemRemovalId, CancellationToken cancellationToken = default);

    Task AddAsync(BillingDocumentPendingItemAssignment assignment, CancellationToken cancellationToken = default);
}
