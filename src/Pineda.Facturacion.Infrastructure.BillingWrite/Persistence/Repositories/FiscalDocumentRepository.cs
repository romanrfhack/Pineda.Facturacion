using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.UseCases.FiscalDocuments;
using Pineda.Facturacion.Domain.Enums;
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
            .Include(x => x.SpecialFieldValues.OrderBy(field => field.DisplayOrder))
            .FirstOrDefaultAsync(x => x.Id == fiscalDocumentId, cancellationToken);
    }

    public Task<FiscalDocument?> GetTrackedByIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
    {
        return _dbContext.FiscalDocuments
            .Include(x => x.Items)
            .Include(x => x.SpecialFieldValues.OrderBy(field => field.DisplayOrder))
            .FirstOrDefaultAsync(x => x.Id == fiscalDocumentId, cancellationToken);
    }

    public Task<FiscalDocument?> GetByBillingDocumentIdAsync(long billingDocumentId, CancellationToken cancellationToken = default)
    {
        return _dbContext.FiscalDocuments
            .AsNoTracking()
            .Include(x => x.Items)
            .Include(x => x.SpecialFieldValues.OrderBy(field => field.DisplayOrder))
            .FirstOrDefaultAsync(x => x.BillingDocumentId == billingDocumentId, cancellationToken);
    }

    public Task<FiscalDocument?> GetTrackedByBillingDocumentIdAsync(long billingDocumentId, CancellationToken cancellationToken = default)
    {
        return _dbContext.FiscalDocuments
            .Include(x => x.Items)
            .Include(x => x.SpecialFieldValues.OrderBy(field => field.DisplayOrder))
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

    public async Task<SearchIssuedFiscalDocumentsResult> SearchIssuedAsync(
        SearchIssuedFiscalDocumentsFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = from fiscalDocument in _dbContext.FiscalDocuments.AsNoTracking()
                    join fiscalStamp in _dbContext.FiscalStamps.AsNoTracking()
                        on fiscalDocument.Id equals fiscalStamp.FiscalDocumentId into stampGroup
                    from stamp in stampGroup.DefaultIfEmpty()
                    select new
                    {
                        FiscalDocument = fiscalDocument,
                        FiscalStamp = stamp
                    };

        if (filter.FromDate.HasValue)
        {
            var fromUtc = DateTime.SpecifyKind(filter.FromDate.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            query = query.Where(x => x.FiscalDocument.IssuedAtUtc >= fromUtc);
        }

        if (filter.ToDate.HasValue)
        {
            var toExclusiveUtc = DateTime.SpecifyKind(filter.ToDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            query = query.Where(x => x.FiscalDocument.IssuedAtUtc < toExclusiveUtc);
        }

        if (!string.IsNullOrWhiteSpace(filter.Status) && Enum.TryParse<FiscalDocumentStatus>(filter.Status, true, out var parsedStatus))
        {
            query = query.Where(x => x.FiscalDocument.Status == parsedStatus);
        }
        else
        {
            query = query.Where(x => x.FiscalDocument.Status == FiscalDocumentStatus.Stamped || x.FiscalDocument.Status == FiscalDocumentStatus.Cancelled);
        }

        if (!string.IsNullOrWhiteSpace(filter.ReceiverRfc))
        {
            var receiverRfc = filter.ReceiverRfc.Trim().ToUpperInvariant();
            query = query.Where(x => x.FiscalDocument.ReceiverRfc.Contains(receiverRfc));
        }

        if (!string.IsNullOrWhiteSpace(filter.ReceiverName))
        {
            var receiverName = filter.ReceiverName.Trim();
            query = query.Where(x => x.FiscalDocument.ReceiverLegalName.Contains(receiverName));
        }

        if (!string.IsNullOrWhiteSpace(filter.Uuid))
        {
            var uuid = filter.Uuid.Trim();
            query = query.Where(x => x.FiscalStamp != null && x.FiscalStamp.Uuid != null && x.FiscalStamp.Uuid.Contains(uuid));
        }

        if (!string.IsNullOrWhiteSpace(filter.Series))
        {
            var series = filter.Series.Trim();
            query = query.Where(x => x.FiscalDocument.Series == series);
        }

        if (!string.IsNullOrWhiteSpace(filter.Folio))
        {
            var folio = filter.Folio.Trim();
            query = query.Where(x => x.FiscalDocument.Folio != null && x.FiscalDocument.Folio.Contains(folio));
        }

        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var search = filter.Query.Trim();
            query = query.Where(x =>
                x.FiscalDocument.ReceiverRfc.Contains(search)
                || x.FiscalDocument.ReceiverLegalName.Contains(search)
                || (x.FiscalDocument.Folio != null && x.FiscalDocument.Folio.Contains(search))
                || (x.FiscalDocument.Series != null && x.FiscalDocument.Series.Contains(search))
                || (x.FiscalStamp != null && x.FiscalStamp.Uuid != null && x.FiscalStamp.Uuid.Contains(search))
                || _dbContext.FiscalDocumentSpecialFieldValues.Any(field => field.FiscalDocumentId == x.FiscalDocument.Id && field.Value.Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(filter.SpecialFieldCode))
        {
            var specialFieldCode = filter.SpecialFieldCode.Trim().ToUpperInvariant();
            query = query.Where(x => _dbContext.FiscalDocumentSpecialFieldValues.Any(
                field => field.FiscalDocumentId == x.FiscalDocument.Id
                    && field.FieldCode == specialFieldCode
                    && (string.IsNullOrWhiteSpace(filter.SpecialFieldValue) || field.Value.Contains(filter.SpecialFieldValue.Trim()))));
        }
        else if (!string.IsNullOrWhiteSpace(filter.SpecialFieldValue))
        {
            var specialFieldValue = filter.SpecialFieldValue.Trim();
            query = query.Where(x => _dbContext.FiscalDocumentSpecialFieldValues.Any(
                field => field.FiscalDocumentId == x.FiscalDocument.Id
                    && field.Value.Contains(specialFieldValue)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 25 : filter.PageSize;
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await query
            .OrderByDescending(x => x.FiscalStamp != null ? x.FiscalStamp.StampedAtUtc : x.FiscalDocument.IssuedAtUtc)
            .ThenByDescending(x => x.FiscalDocument.IssuedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new IssuedFiscalDocumentListItem
            {
                FiscalDocumentId = x.FiscalDocument.Id,
                BillingDocumentId = x.FiscalDocument.BillingDocumentId,
                Status = x.FiscalDocument.Status.ToString(),
                IssuedAtUtc = x.FiscalDocument.IssuedAtUtc,
                StampedAtUtc = x.FiscalStamp != null ? x.FiscalStamp.StampedAtUtc : null,
                IssuerRfc = x.FiscalDocument.IssuerRfc,
                IssuerLegalName = x.FiscalDocument.IssuerLegalName,
                Series = x.FiscalDocument.Series ?? string.Empty,
                Folio = x.FiscalDocument.Folio ?? string.Empty,
                Uuid = x.FiscalStamp != null ? x.FiscalStamp.Uuid : null,
                ReceiverRfc = x.FiscalDocument.ReceiverRfc,
                ReceiverLegalName = x.FiscalDocument.ReceiverLegalName,
                ReceiverCfdiUseCode = x.FiscalDocument.ReceiverCfdiUseCode,
                PaymentMethodSat = x.FiscalDocument.PaymentMethodSat,
                PaymentFormSat = x.FiscalDocument.PaymentFormSat,
                DocumentType = x.FiscalDocument.DocumentType,
                Total = x.FiscalDocument.Total
            })
            .ToListAsync(cancellationToken);

        return new SearchIssuedFiscalDocumentsResult
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            Items = items
        };
    }

    public async Task AddAsync(FiscalDocument fiscalDocument, CancellationToken cancellationToken = default)
    {
        await _dbContext.FiscalDocuments.AddAsync(fiscalDocument, cancellationToken);
    }

    public async Task<IReadOnlyList<IssuedFiscalDocumentSpecialFieldOption>> GetIssuedSpecialFieldOptionsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.FiscalReceiverSpecialFieldDefinitions
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Label)
            .Select(x => new IssuedFiscalDocumentSpecialFieldOption
            {
                Code = x.Code,
                Label = x.Label
            })
            .Distinct()
            .ToListAsync(cancellationToken);
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
