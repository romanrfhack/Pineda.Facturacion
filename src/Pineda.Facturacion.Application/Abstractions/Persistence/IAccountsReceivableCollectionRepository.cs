using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface IAccountsReceivableCollectionRepository
{
    Task AddCommitmentAsync(CollectionCommitment commitment, CancellationToken cancellationToken = default);

    Task AddNoteAsync(CollectionNote note, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CollectionCommitment>> ListCommitmentsByInvoiceIdAsync(long accountsReceivableInvoiceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CollectionCommitment>> ListCommitmentsByInvoiceIdsAsync(IReadOnlyCollection<long> accountsReceivableInvoiceIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CollectionCommitment>> GetTrackedOpenCommitmentsByInvoiceIdsAsync(IReadOnlyCollection<long> accountsReceivableInvoiceIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CollectionNote>> ListNotesByInvoiceIdAsync(long accountsReceivableInvoiceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CollectionNote>> ListNotesByInvoiceIdsAsync(IReadOnlyCollection<long> accountsReceivableInvoiceIds, CancellationToken cancellationToken = default);
}
