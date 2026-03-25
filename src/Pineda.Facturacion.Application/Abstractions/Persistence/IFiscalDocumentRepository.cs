using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface IFiscalDocumentRepository
{
    Task<FiscalDocument?> GetByIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default);

    Task<FiscalDocument?> GetTrackedByIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default);

    Task<FiscalDocument?> GetByBillingDocumentIdAsync(long billingDocumentId, CancellationToken cancellationToken = default);

    Task<bool> ExistsByIssuerSeriesAndFolioAsync(
        string issuerRfc,
        string series,
        string folio,
        long? excludeFiscalDocumentId = null,
        CancellationToken cancellationToken = default);

    Task<int?> GetLastUsedFolioAsync(string issuerRfc, string series, CancellationToken cancellationToken = default);

    Task AddAsync(FiscalDocument fiscalDocument, CancellationToken cancellationToken = default);
}
