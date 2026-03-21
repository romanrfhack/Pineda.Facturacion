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
        return Query()
            .FirstOrDefaultAsync(x => x.BillingDocumentId == billingDocumentId, cancellationToken);
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
            join fiscalDocument in _dbContext.FiscalDocuments.AsNoTracking()
                on billingDocument.Id equals fiscalDocument.BillingDocumentId into fiscalDocuments
            from fiscalDocument in fiscalDocuments.DefaultIfEmpty()
            select new BillingDocumentLookupModel
            {
                BillingDocumentId = billingDocument.Id,
                SalesOrderId = billingDocument.SalesOrderId,
                LegacyOrderId = salesOrder.LegacyOrderNumber,
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
