using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

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
            .ToList();

        var importRecords = await _dbContext.LegacyImportRecords
            .AsNoTracking()
            .Where(x =>
                x.SourceSystem == LegacySourceSystem
                && x.SourceTable == LegacyOrdersSourceTable
                && normalizedIds.Contains(x.SourceDocumentId))
            .ToListAsync(cancellationToken);
        var matchedImportRecords = importRecords.ToArray();

        if (matchedImportRecords.Length == 0)
        {
            return new Dictionary<string, ImportedLegacyOrderLookupModel>(StringComparer.OrdinalIgnoreCase);
        }

        var importRecordIds = matchedImportRecords
            .Select(x => x.Id)
            .Distinct()
            .ToList();
        var directBillingDocumentIds = matchedImportRecords
            .Where(x => x.BillingDocumentId.HasValue)
            .Select(x => x.BillingDocumentId!.Value)
            .Distinct()
            .ToList();
        var salesOrders = (await _dbContext.SalesOrders
            .AsNoTracking()
            .Where(x => importRecordIds.Contains(x.LegacyImportRecordId))
            .ToListAsync(cancellationToken))
            .ToArray();

        var salesOrderIds = salesOrders.Select(x => x.Id).ToList();
        var billingDocuments = (await _dbContext.BillingDocuments
            .AsNoTracking()
            .Where(x => salesOrderIds.Contains(x.SalesOrderId) || directBillingDocumentIds.Contains(x.Id))
            .ToListAsync(cancellationToken))
            .ToArray();

        var billingDocumentIds = billingDocuments.Select(x => x.Id).ToList();
        var fiscalDocuments = (await _dbContext.FiscalDocuments
            .AsNoTracking()
            .Where(x => billingDocumentIds.Contains(x.BillingDocumentId))
            .ToListAsync(cancellationToken))
            .ToArray();
        var fiscalDocumentIds = fiscalDocuments.Select(x => x.Id).ToList();
        var fiscalStamps = fiscalDocumentIds.Count == 0
            ? []
            : (await _dbContext.FiscalStamps
                .AsNoTracking()
                .Where(x => fiscalDocumentIds.Contains(x.FiscalDocumentId) && !string.IsNullOrWhiteSpace(x.Uuid))
                .ToListAsync(cancellationToken))
                .ToArray();

        return matchedImportRecords
            .Select(importRecord =>
            {
                var salesOrder = salesOrders.FirstOrDefault(x => x.LegacyImportRecordId == importRecord.Id);
                var relatedBillingDocuments = billingDocuments
                    .Where(x => (salesOrder is not null && x.SalesOrderId == salesOrder.Id)
                        || (importRecord.BillingDocumentId.HasValue && x.Id == importRecord.BillingDocumentId.Value))
                    .ToArray();
                var billingDocument = SelectOperationalBillingDocument(relatedBillingDocuments, fiscalDocuments);
                var fiscalDocument = billingDocument is null
                    ? null
                    : fiscalDocuments
                        .Where(x => x.BillingDocumentId == billingDocument.Id && x.Status != FiscalDocumentStatus.DiscardedUnstamped)
                        .OrderByDescending(x => x.Id)
                        .FirstOrDefault();
                var fiscalStamp = fiscalDocument is null
                    ? null
                    : fiscalStamps
                        .Where(x => x.FiscalDocumentId == fiscalDocument.Id)
                        .OrderByDescending(x => x.CreatedAtUtc)
                        .ThenByDescending(x => x.Id)
                        .FirstOrDefault();

                return new ImportedLegacyOrderLookupModel
                {
                    LegacyOrderId = importRecord.SourceDocumentId,
                    SalesOrderId = salesOrder?.Id,
                    SalesOrderStatus = salesOrder?.Status.ToString(),
                    BillingDocumentId = billingDocument?.Id,
                    BillingDocumentStatus = billingDocument?.Status.ToString(),
                    FiscalDocumentId = fiscalDocument?.Id,
                    FiscalDocumentStatus = fiscalDocument?.Status.ToString(),
                    FiscalUuid = fiscalStamp?.Uuid,
                    ImportStatus = importRecord.ImportStatus.ToString(),
                    ImportedAtUtc = importRecord.ImportedAtUtc,
                    ExistingSourceHash = importRecord.SourceHash
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

    private static BillingDocument? SelectOperationalBillingDocument(
        IReadOnlyCollection<BillingDocument> relatedBillingDocuments,
        IReadOnlyCollection<FiscalDocument> fiscalDocuments)
    {
        return relatedBillingDocuments
            .Select(billingDocument => new
            {
                BillingDocument = billingDocument,
                FiscalDocument = fiscalDocuments
                    .Where(x => x.BillingDocumentId == billingDocument.Id && x.Status != FiscalDocumentStatus.DiscardedUnstamped)
                    .OrderByDescending(x => x.Id)
                    .FirstOrDefault()
            })
            .Where(x => x.FiscalDocument is null || x.FiscalDocument.Status != FiscalDocumentStatus.Cancelled)
            .OrderByDescending(x => x.BillingDocument.Id)
            .Select(x => x.BillingDocument)
            .FirstOrDefault();
    }
}
