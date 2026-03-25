using Pineda.Facturacion.Application.Abstractions.Persistence;

namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public sealed class SearchIssuedFiscalDocumentsService
{
    private readonly IFiscalDocumentRepository _fiscalDocumentRepository;

    public SearchIssuedFiscalDocumentsService(IFiscalDocumentRepository fiscalDocumentRepository)
    {
        _fiscalDocumentRepository = fiscalDocumentRepository;
    }

    public async Task<SearchIssuedFiscalDocumentsResult> ExecuteAsync(
        SearchIssuedFiscalDocumentsFilter filter,
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

        if (filter.FromDate.HasValue && filter.ToDate.HasValue && filter.FromDate.Value > filter.ToDate.Value)
        {
            throw new ArgumentException("FromDate cannot be greater than ToDate.", nameof(filter));
        }

        return await _fiscalDocumentRepository.SearchIssuedAsync(
            new SearchIssuedFiscalDocumentsFilter
            {
                Page = normalizedPage,
                PageSize = normalizedPageSize,
                FromDate = filter.FromDate,
                ToDate = filter.ToDate,
                ReceiverRfc = NormalizeOptionalText(filter.ReceiverRfc),
                ReceiverName = NormalizeOptionalText(filter.ReceiverName),
                Uuid = NormalizeOptionalText(filter.Uuid),
                Series = NormalizeOptionalText(filter.Series),
                Folio = NormalizeOptionalText(filter.Folio),
                Status = NormalizeOptionalText(filter.Status),
                Query = NormalizeOptionalText(filter.Query)
            },
            cancellationToken);
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
