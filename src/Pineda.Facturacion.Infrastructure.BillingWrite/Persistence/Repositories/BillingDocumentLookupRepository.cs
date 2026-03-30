using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.Abstractions.Persistence;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

public sealed class BillingDocumentLookupRepository : IBillingDocumentLookupRepository
{
    private readonly BillingDbContext _dbContext;

    public BillingDocumentLookupRepository(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<BillingDocumentLookupModel?> GetByIdAsync(long billingDocumentId, CancellationToken cancellationToken = default)
    {
        return _dbContext.BillingDocuments
            .AsNoTracking()
            .Where(x => x.Id == billingDocumentId)
            .Select(billingDocument => new BillingDocumentLookupModel
            {
                BillingDocumentId = billingDocument.Id,
                SalesOrderId = billingDocument.SalesOrderId,
                LegacyOrderId =
                    (from salesOrder in _dbContext.SalesOrders
                     join legacyImportRecord in _dbContext.LegacyImportRecords
                         on salesOrder.LegacyImportRecordId equals legacyImportRecord.Id
                     where salesOrder.Id == billingDocument.SalesOrderId
                     select string.IsNullOrEmpty(legacyImportRecord.SourceDocumentId)
                         ? salesOrder.LegacyOrderNumber
                         : string.IsNullOrEmpty(salesOrder.LegacyOrderNumber)
                             ? legacyImportRecord.SourceDocumentId
                             : legacyImportRecord.SourceDocumentId + "-" + salesOrder.LegacyOrderNumber)
                    .FirstOrDefault() ?? string.Empty,
                Status = billingDocument.Status.ToString(),
                DocumentType = billingDocument.DocumentType,
                CurrencyCode = billingDocument.CurrencyCode,
                Total = billingDocument.Total,
                CreatedAtUtc = billingDocument.CreatedAtUtc,
                FiscalDocumentId = _dbContext.FiscalDocuments
                    .Where(fiscalDocument => fiscalDocument.BillingDocumentId == billingDocument.Id)
                    .Select(fiscalDocument => (long?)fiscalDocument.Id)
                    .FirstOrDefault(),
                FiscalDocumentStatus = _dbContext.FiscalDocuments
                    .Where(fiscalDocument => fiscalDocument.BillingDocumentId == billingDocument.Id)
                    .Select(fiscalDocument => fiscalDocument.Status.ToString())
                    .FirstOrDefault(),
                Items = billingDocument.Items
                    .OrderBy(item => item.LineNumber)
                    .Select(item => new BillingDocumentLookupItemModel
                    {
                        LineNumber = item.LineNumber,
                        ProductInternalCode = item.ProductInternalCode,
                        Description = item.Description
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BillingDocumentLookupModel>> SearchAsync(string query, int take = 5, CancellationToken cancellationToken = default)
    {
        var normalizedQuery = query.Trim();
        var parsedNumeric = long.TryParse(normalizedQuery, out var numericValue);

        var billingDocuments = Query();

        if (parsedNumeric)
        {
            billingDocuments = billingDocuments.Where(x =>
                x.BillingDocumentId == numericValue
                || x.SalesOrderId == numericValue
                || x.LegacyOrderId.Contains(normalizedQuery));
        }
        else
        {
            billingDocuments = billingDocuments.Where(x => x.LegacyOrderId.Contains(normalizedQuery));
        }

        var results = await billingDocuments
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(Math.Max(1, take))
            .ToListAsync(cancellationToken);

        return results;
    }

    private IQueryable<BillingDocumentLookupModel> Query()
    {
        return
            from billingDocument in _dbContext.BillingDocuments.AsNoTracking()
            join salesOrder in _dbContext.SalesOrders.AsNoTracking()
                on billingDocument.SalesOrderId equals salesOrder.Id
            join legacyImportRecord in _dbContext.LegacyImportRecords.AsNoTracking()
                on salesOrder.LegacyImportRecordId equals legacyImportRecord.Id
            join fiscalDocument in _dbContext.FiscalDocuments.AsNoTracking()
                on billingDocument.Id equals fiscalDocument.BillingDocumentId into fiscalDocuments
            from fiscalDocument in fiscalDocuments.DefaultIfEmpty()
            select new BillingDocumentLookupModel
            {
                BillingDocumentId = billingDocument.Id,
                SalesOrderId = billingDocument.SalesOrderId,
                LegacyOrderId = string.IsNullOrEmpty(legacyImportRecord.SourceDocumentId)
                    ? salesOrder.LegacyOrderNumber
                    : string.IsNullOrEmpty(salesOrder.LegacyOrderNumber)
                        ? legacyImportRecord.SourceDocumentId
                        : legacyImportRecord.SourceDocumentId + "-" + salesOrder.LegacyOrderNumber,
                Status = billingDocument.Status.ToString(),
                DocumentType = billingDocument.DocumentType,
                CurrencyCode = billingDocument.CurrencyCode,
                Total = billingDocument.Total,
                CreatedAtUtc = billingDocument.CreatedAtUtc,
                FiscalDocumentId = fiscalDocument != null ? fiscalDocument.Id : null,
                FiscalDocumentStatus = fiscalDocument != null ? fiscalDocument.Status.ToString() : null
            };
    }
}
