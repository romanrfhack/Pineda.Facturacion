using Pineda.Facturacion.Application.Abstractions.Persistence;

namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public sealed class ListCollectionCommitmentsByInvoiceIdService
{
    private readonly IAccountsReceivableInvoiceRepository _accountsReceivableInvoiceRepository;
    private readonly IAccountsReceivableCollectionRepository _collectionRepository;

    public ListCollectionCommitmentsByInvoiceIdService(
        IAccountsReceivableInvoiceRepository accountsReceivableInvoiceRepository,
        IAccountsReceivableCollectionRepository collectionRepository)
    {
        _accountsReceivableInvoiceRepository = accountsReceivableInvoiceRepository;
        _collectionRepository = collectionRepository;
    }

    public async Task<IReadOnlyList<CollectionCommitmentProjection>> ExecuteAsync(long accountsReceivableInvoiceId, CancellationToken cancellationToken = default)
    {
        var invoice = await _accountsReceivableInvoiceRepository.GetTrackedByIdAsync(accountsReceivableInvoiceId, cancellationToken);
        if (invoice is null)
        {
            return [];
        }

        var commitments = await _collectionRepository.ListCommitmentsByInvoiceIdAsync(accountsReceivableInvoiceId, cancellationToken);
        var now = DateTime.UtcNow;
        return commitments
            .Select(x => AccountsReceivableCollectionProjectionBuilder.MapCommitment(x, invoice.OutstandingBalance, invoice.Status.ToString(), now))
            .ToList();
    }
}
