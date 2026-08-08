using Pineda.Facturacion.Application.Abstractions.Persistence;

namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public sealed class SearchPendingFiscalDocumentsService
{
    private readonly IPendingFiscalDocumentRepository _repository;

    public SearchPendingFiscalDocumentsService(IPendingFiscalDocumentRepository repository)
    {
        _repository = repository;
    }

    public Task<SearchPendingFiscalDocumentsResult> ExecuteAsync(
        SearchPendingFiscalDocumentsFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var normalizedPage = filter.Page < 1 ? 1 : filter.Page;
        var normalizedPageSize = filter.PageSize switch
        {
            < 1 => 25,
            > 50 => 50,
            _ => filter.PageSize
        };

        return _repository.SearchAsync(
            new SearchPendingFiscalDocumentsFilter
            {
                Page = normalizedPage,
                PageSize = normalizedPageSize,
                Query = string.IsNullOrWhiteSpace(filter.Query) ? null : filter.Query.Trim(),
                WorkFilter = filter.WorkFilter,
                Sort = filter.Sort
            },
            cancellationToken);
    }
}
