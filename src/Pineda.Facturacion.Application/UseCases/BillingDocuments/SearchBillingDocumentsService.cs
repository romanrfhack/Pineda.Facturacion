using Pineda.Facturacion.Application.Abstractions.Persistence;

namespace Pineda.Facturacion.Application.UseCases.BillingDocuments;

public sealed class SearchBillingDocumentsService
{
    private readonly IBillingDocumentLookupRepository _repository;

    public SearchBillingDocumentsService(IBillingDocumentLookupRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<BillingDocumentLookupModel>> ExecuteAsync(string query, int take = 5, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult<IReadOnlyList<BillingDocumentLookupModel>>([]);
        }

        return _repository.SearchAsync(query.Trim(), take, cancellationToken);
    }
}
