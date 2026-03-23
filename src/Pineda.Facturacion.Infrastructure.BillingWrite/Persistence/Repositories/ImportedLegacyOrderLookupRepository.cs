using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.Abstractions.Persistence;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

public sealed class ImportedLegacyOrderLookupRepository : IImportedLegacyOrderLookupRepository
{
    private const string LegacySourceSystem = "legacy";
    private const string LegacyOrdersSourceTable = "pedidos";

    private readonly BillingDbContext _dbContext;

    public ImportedLegacyOrderLookupRepository(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyDictionary<string, ImportedLegacyOrderLookupModel>> GetByLegacyOrderIdsAsync(
        IReadOnlyCollection<string> legacyOrderIds,
        CancellationToken cancellationToken = default)
    {
        if (legacyOrderIds.Count == 0)
        {
            return new Dictionary<string, ImportedLegacyOrderLookupModel>(StringComparer.OrdinalIgnoreCase);
        }

        var normalizedIds = legacyOrderIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var importRecords = await _dbContext.LegacyImportRecords
            .AsNoTracking()
            .Where(x => x.SourceSystem == LegacySourceSystem && x.SourceTable == LegacyOrdersSourceTable)
            .ToListAsync(cancellationToken);

        var matchedImportRecords = importRecords
            .Where(x => normalizedIds.Contains(x.SourceDocumentId, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        if (matchedImportRecords.Length == 0)
        {
            return new Dictionary<string, ImportedLegacyOrderLookupModel>(StringComparer.OrdinalIgnoreCase);
        }

        var importRecordIds = matchedImportRecords.Select(x => x.Id).ToArray();
        var salesOrders = (await _dbContext.SalesOrders
            .AsNoTracking()
            .ToListAsync(cancellationToken))
            .Where(x => importRecordIds.Contains(x.LegacyImportRecordId))
            .ToArray();

        var salesOrderIds = salesOrders.Select(x => x.Id).ToArray();
        var billingDocuments = (await _dbContext.BillingDocuments
            .AsNoTracking()
            .ToListAsync(cancellationToken))
            .Where(x => salesOrderIds.Contains(x.SalesOrderId))
            .ToArray();

        var billingDocumentIds = billingDocuments.Select(x => x.Id).ToArray();
        var fiscalDocuments = (await _dbContext.FiscalDocuments
            .AsNoTracking()
            .ToListAsync(cancellationToken))
            .Where(x => billingDocumentIds.Contains(x.BillingDocumentId))
            .ToArray();

        return matchedImportRecords
            .Select(importRecord =>
            {
                var salesOrder = salesOrders.FirstOrDefault(x => x.LegacyImportRecordId == importRecord.Id);
                var billingDocument = salesOrder is null
                    ? null
                    : billingDocuments.FirstOrDefault(x => x.SalesOrderId == salesOrder.Id);
                var fiscalDocument = billingDocument is null
                    ? null
                    : fiscalDocuments.FirstOrDefault(x => x.BillingDocumentId == billingDocument.Id);

                return new ImportedLegacyOrderLookupModel
                {
                    LegacyOrderId = importRecord.SourceDocumentId,
                    SalesOrderId = salesOrder?.Id,
                    BillingDocumentId = billingDocument?.Id,
                    BillingDocumentStatus = billingDocument?.Status.ToString(),
                    FiscalDocumentId = fiscalDocument?.Id,
                    FiscalDocumentStatus = fiscalDocument?.Status.ToString(),
                    ImportStatus = importRecord.ImportStatus.ToString()
                };
            })
            .GroupBy(x => x.LegacyOrderId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                x => x.Key,
                x => x
                    .OrderByDescending(y => y.BillingDocumentId.HasValue)
                    .ThenByDescending(y => y.FiscalDocumentId.HasValue)
                    .First(),
                StringComparer.OrdinalIgnoreCase);
    }
}
