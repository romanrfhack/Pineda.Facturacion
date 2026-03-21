using Pineda.Facturacion.Application.Abstractions.Persistence;

namespace Pineda.Facturacion.Application.UseCases.BillingDocuments;

public sealed class GetBillingDocumentLookupByIdService
{
    private readonly IBillingDocumentLookupRepository _repository;

    public GetBillingDocumentLookupByIdService(IBillingDocumentLookupRepository repository)
    {
        _repository = repository;
    }

    public Task<BillingDocumentLookupModel?> ExecuteAsync(long billingDocumentId, CancellationToken cancellationToken = default)
    {
        return _repository.GetByIdAsync(billingDocumentId, cancellationToken);
    }
}
