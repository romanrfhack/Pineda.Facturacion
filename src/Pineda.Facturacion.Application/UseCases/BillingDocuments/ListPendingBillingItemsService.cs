using Pineda.Facturacion.Application.Abstractions.Persistence;

namespace Pineda.Facturacion.Application.UseCases.BillingDocuments;

public sealed class ListPendingBillingItemsService
{
    private readonly IBillingDocumentItemRemovalRepository _billingDocumentItemRemovalRepository;

    public ListPendingBillingItemsService(IBillingDocumentItemRemovalRepository billingDocumentItemRemovalRepository)
    {
        _billingDocumentItemRemovalRepository = billingDocumentItemRemovalRepository;
    }

    public Task<IReadOnlyList<PendingBillingItemLookupModel>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        return _billingDocumentItemRemovalRepository.ListAvailablePendingBillingLookupAsync(cancellationToken);
    }
}
