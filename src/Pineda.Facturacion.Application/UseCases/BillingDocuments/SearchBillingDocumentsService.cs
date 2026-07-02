using Pineda.Facturacion.Application.Abstractions.Persistence;

namespace Pineda.Facturacion.Application.UseCases.BillingDocuments;

public sealed class SearchBillingDocumentsService
{
    private const int MaxTakePerGroup = 5;
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

    public Task<GroupedBillingDocumentSearchModel> ExecuteGroupedAsync(
        string query,
        int takePerGroup = MaxTakePerGroup,
        CancellationToken cancellationToken = default)
    {
        var normalizedTakePerGroup = Math.Clamp(takePerGroup, 1, MaxTakePerGroup);
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult(new GroupedBillingDocumentSearchModel
            {
                Query = string.Empty,
                TakePerGroup = normalizedTakePerGroup
            });
        }

        return _repository.SearchGroupedAsync(query.Trim(), normalizedTakePerGroup, cancellationToken);
    }
}
