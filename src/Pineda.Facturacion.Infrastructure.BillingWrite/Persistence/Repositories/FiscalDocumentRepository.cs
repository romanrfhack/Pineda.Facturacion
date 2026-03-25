using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

public class FiscalDocumentRepository : IFiscalDocumentRepository
{
    private readonly BillingDbContext _dbContext;

    public FiscalDocumentRepository(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<FiscalDocument?> GetByIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
    {
        return _dbContext.FiscalDocuments
            .AsNoTracking()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == fiscalDocumentId, cancellationToken);
    }

    public Task<FiscalDocument?> GetTrackedByIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
    {
        return _dbContext.FiscalDocuments
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == fiscalDocumentId, cancellationToken);
    }

    public Task<FiscalDocument?> GetByBillingDocumentIdAsync(long billingDocumentId, CancellationToken cancellationToken = default)
    {
        return _dbContext.FiscalDocuments
            .AsNoTracking()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.BillingDocumentId == billingDocumentId, cancellationToken);
    }

    public Task<bool> ExistsByIssuerSeriesAndFolioAsync(
        string issuerRfc,
        string series,
        string folio,
        long? excludeFiscalDocumentId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedIssuerRfc = NormalizeRfc(issuerRfc);
        var normalizedSeries = NormalizeOptionalText(series) ?? string.Empty;
        var normalizedFolio = NormalizeRequiredText(folio);

        return _dbContext.FiscalDocuments
            .AsNoTracking()
            .AnyAsync(
                x => x.IssuerRfc == normalizedIssuerRfc
                    && x.Series == normalizedSeries
                    && x.Folio == normalizedFolio
                    && (!excludeFiscalDocumentId.HasValue || x.Id != excludeFiscalDocumentId.Value),
                cancellationToken);
    }

    public async Task<int?> GetLastUsedFolioAsync(string issuerRfc, string series, CancellationToken cancellationToken = default)
    {
        var normalizedIssuerRfc = NormalizeRfc(issuerRfc);
        var normalizedSeries = NormalizeOptionalText(series) ?? string.Empty;

        var folios = await _dbContext.FiscalDocuments
            .AsNoTracking()
            .Where(x => x.IssuerRfc == normalizedIssuerRfc && x.Series == normalizedSeries && x.Folio != null)
            .Select(x => x.Folio!)
            .ToListAsync(cancellationToken);

        return folios
            .Select(static folio => int.TryParse(folio, out var parsed) ? parsed : (int?)null)
            .Where(static parsed => parsed.HasValue)
            .Select(static parsed => parsed!.Value)
            .DefaultIfEmpty()
            .Max();
    }

    public async Task AddAsync(FiscalDocument fiscalDocument, CancellationToken cancellationToken = default)
    {
        await _dbContext.FiscalDocuments.AddAsync(fiscalDocument, cancellationToken);
    }

    private static string NormalizeRfc(string value)
    {
        return NormalizeRequiredText(value).ToUpperInvariant();
    }

    private static string NormalizeRequiredText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", nameof(value));
        }

        return value.Trim();
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
