using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public sealed class SynchronizeAccountsReceivableCollectionStateService
{
    private readonly IAccountsReceivableCollectionRepository _collectionRepository;

    public SynchronizeAccountsReceivableCollectionStateService(IAccountsReceivableCollectionRepository collectionRepository)
    {
        _collectionRepository = collectionRepository;
    }

    public async Task FulfillOpenCommitmentsAsync(IReadOnlyCollection<long> accountsReceivableInvoiceIds, DateTime now, CancellationToken cancellationToken = default)
    {
        var commitments = await _collectionRepository.GetTrackedOpenCommitmentsByInvoiceIdsAsync(accountsReceivableInvoiceIds, cancellationToken);
        foreach (var commitment in commitments)
        {
            commitment.Status = CollectionCommitmentStatus.Fulfilled;
            commitment.UpdatedAtUtc = now;
        }
    }

    public async Task CancelOpenCommitmentsAsync(IReadOnlyCollection<long> accountsReceivableInvoiceIds, DateTime now, CancellationToken cancellationToken = default)
    {
        var commitments = await _collectionRepository.GetTrackedOpenCommitmentsByInvoiceIdsAsync(accountsReceivableInvoiceIds, cancellationToken);
        foreach (var commitment in commitments)
        {
            commitment.Status = CollectionCommitmentStatus.Cancelled;
            commitment.UpdatedAtUtc = now;
        }
    }
}
