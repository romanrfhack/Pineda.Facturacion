using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Application.UseCases.FiscalDocuments;

namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface IFiscalDocumentRepository
{
    Task<FiscalDocument?> GetByIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default);

    Task<FiscalDocument?> GetTrackedByIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default);

    Task<FiscalDocument?> GetByBillingDocumentIdAsync(long billingDocumentId, CancellationToken cancellationToken = default);

    Task<FiscalDocument?> GetTrackedByBillingDocumentIdAsync(long billingDocumentId, CancellationToken cancellationToken = default)
    {
        return GetByBillingDocumentIdAsync(billingDocumentId, cancellationToken);
    }

    Task<bool> ExistsByIssuerSeriesAndFolioAsync(
        string issuerRfc,
        string series,
        string folio,
        long? excludeFiscalDocumentId = null,
        CancellationToken cancellationToken = default);

    Task<int?> GetLastUsedFolioAsync(string issuerRfc, string series, CancellationToken cancellationToken = default);

    Task<SearchIssuedFiscalDocumentsResult> SearchIssuedAsync(
        SearchIssuedFiscalDocumentsFilter filter,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new SearchIssuedFiscalDocumentsResult
        {
            Page = filter.Page,
            PageSize = filter.PageSize,
            TotalCount = 0,
            TotalPages = 0,
            Items = []
        });
    }

    Task<IReadOnlyList<IssuedFiscalDocumentSpecialFieldOption>> GetIssuedSpecialFieldOptionsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<IssuedFiscalDocumentSpecialFieldOption>>([]);
    }

    Task AddAsync(FiscalDocument fiscalDocument, CancellationToken cancellationToken = default);
}
