using Pineda.Facturacion.Application.Abstractions.Persistence;

namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public sealed class ListCollectionNotesByInvoiceIdService
{
    private readonly IAccountsReceivableCollectionRepository _collectionRepository;

    public ListCollectionNotesByInvoiceIdService(IAccountsReceivableCollectionRepository collectionRepository)
    {
        _collectionRepository = collectionRepository;
    }

    public async Task<IReadOnlyList<CollectionNoteProjection>> ExecuteAsync(long accountsReceivableInvoiceId, CancellationToken cancellationToken = default)
    {
        var notes = await _collectionRepository.ListNotesByInvoiceIdAsync(accountsReceivableInvoiceId, cancellationToken);
        return notes.Select(AccountsReceivableCollectionProjectionBuilder.MapNote).ToList();
    }
}
