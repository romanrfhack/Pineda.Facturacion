using Pineda.Facturacion.Application.Abstractions.Hashing;
using Pineda.Facturacion.Application.Abstractions.Legacy;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.UseCases.ImportLegacyOrder;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.ImportLegacyOrderPreview;

public sealed class PreviewLegacyOrderImportService
{
    private const string LegacySourceSystem = "legacy";
    private const string LegacyOrdersSourceTable = "pedidos";

    private readonly IContentHashGenerator _contentHashGenerator;
    private readonly IImportedLegacyOrderLookupRepository _importedLegacyOrderLookupRepository;
    private readonly ILegacyImportRecordRepository _legacyImportRecordRepository;
    private readonly ILegacyOrderReader _legacyOrderReader;
    private readonly ISalesOrderSnapshotRepository _salesOrderSnapshotRepository;

    public PreviewLegacyOrderImportService(
        ILegacyOrderReader legacyOrderReader,
        ILegacyImportRecordRepository legacyImportRecordRepository,
        IImportedLegacyOrderLookupRepository importedLegacyOrderLookupRepository,
        ISalesOrderSnapshotRepository salesOrderSnapshotRepository,
        IContentHashGenerator contentHashGenerator)
    {
        _legacyOrderReader = legacyOrderReader;
        _legacyImportRecordRepository = legacyImportRecordRepository;
        _importedLegacyOrderLookupRepository = importedLegacyOrderLookupRepository;
        _salesOrderSnapshotRepository = salesOrderSnapshotRepository;
        _contentHashGenerator = contentHashGenerator;
    }

    public async Task<PreviewLegacyOrderImportResult> ExecuteAsync(string legacyOrderId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(legacyOrderId))
        {
            return Failure("Legacy order id is required.");
        }

        var importRecord = await _legacyImportRecordRepository.GetBySourceDocumentAsync(
            LegacySourceSystem,
            LegacyOrdersSourceTable,
            legacyOrderId,
            cancellationToken);

        if (importRecord is null)
        {
            return Failure($"Legacy order '{legacyOrderId}' has not been imported yet.");
        }

        var currentLegacyOrder = await _legacyOrderReader.GetByIdAsync(legacyOrderId, cancellationToken);
        if (currentLegacyOrder is null)
        {
            return Failure($"Legacy order '{legacyOrderId}' was not found.");
        }

        var currentSourceHash = _contentHashGenerator.GenerateHash(currentLegacyOrder);
        var importedLookup = await _importedLegacyOrderLookupRepository.GetByLegacyOrderIdsAsync([legacyOrderId], cancellationToken);
        importedLookup.TryGetValue(legacyOrderId, out var existingOrder);

        if (existingOrder?.SalesOrderId is null)
        {
            return Failure($"Imported snapshot for legacy order '{legacyOrderId}' was not found.");
        }

        var existingSnapshot = await _salesOrderSnapshotRepository.GetByIdWithItemsAsync(existingOrder.SalesOrderId.Value, cancellationToken);
        if (existingSnapshot is null)
        {
            return Failure($"Sales order snapshot '{existingOrder.SalesOrderId.Value}' was not found.");
        }

        var currentSnapshot = LegacyOrderSnapshotMapper.MapToSalesOrder(currentLegacyOrder, existingSnapshot.LegacyImportRecordId);
        var lineDiff = BuildLineDiff(existingSnapshot, currentSnapshot);
        var changedOrderFields = BuildChangedOrderFields(existingSnapshot, currentSnapshot);
        var hasChanges = !string.Equals(importRecord.SourceHash, currentSourceHash, StringComparison.Ordinal)
            || lineDiff.LineChanges.Count > 0
            || changedOrderFields.Count > 0;

        var eligibility = LegacyOrderReimportPolicy.BuildEligibility(existingOrder, hasChanges);

        return new PreviewLegacyOrderImportResult
        {
            IsSuccess = true,
            LegacyOrderId = legacyOrderId,
            ExistingSalesOrderId = existingOrder.SalesOrderId,
            ExistingSalesOrderStatus = existingOrder.SalesOrderStatus,
            ExistingBillingDocumentId = existingOrder.BillingDocumentId,
            ExistingBillingDocumentStatus = existingOrder.BillingDocumentStatus,
            ExistingFiscalDocumentId = existingOrder.FiscalDocumentId,
            ExistingFiscalDocumentStatus = existingOrder.FiscalDocumentStatus,
            FiscalUuid = existingOrder.FiscalUuid,
            ExistingSourceHash = importRecord.SourceHash,
            CurrentSourceHash = currentSourceHash,
            HasChanges = hasChanges,
            ChangedOrderFields = changedOrderFields,
            ChangeSummary = new PreviewLegacyOrderImportChangeSummary
            {
                AddedLines = lineDiff.AddedLines,
                RemovedLines = lineDiff.RemovedLines,
                ModifiedLines = lineDiff.ModifiedLines,
                UnchangedLines = lineDiff.UnchangedLines,
                OldSubtotal = existingSnapshot.Subtotal,
                NewSubtotal = currentSnapshot.Subtotal,
                OldTotal = existingSnapshot.Total,
                NewTotal = currentSnapshot.Total
            },
            LineChanges = lineDiff.LineChanges,
            ReimportEligibility = eligibility,
            AllowedActions = LegacyOrderReimportPolicy.BuildAllowedActions(existingOrder)
        };
    }

    private static PreviewLegacyOrderImportResult Failure(string errorMessage)
    {
        return new PreviewLegacyOrderImportResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }

    private static List<string> BuildChangedOrderFields(Domain.Entities.SalesOrder existingSnapshot, Domain.Entities.SalesOrder currentSnapshot)
    {
        var changedFields = new List<string>();

        Compare(existingSnapshot.LegacyOrderNumber, currentSnapshot.LegacyOrderNumber, "legacyOrderNumber");
        Compare(existingSnapshot.LegacyOrderType, currentSnapshot.LegacyOrderType, "legacyOrderType");
        Compare(existingSnapshot.CustomerLegacyId, currentSnapshot.CustomerLegacyId, "customerLegacyId");
        Compare(existingSnapshot.CustomerName, currentSnapshot.CustomerName, "customerName");
        Compare(existingSnapshot.CustomerRfc, currentSnapshot.CustomerRfc, "customerRfc");
        Compare(existingSnapshot.PaymentCondition, currentSnapshot.PaymentCondition, "paymentCondition");
        Compare(existingSnapshot.PriceListCode, currentSnapshot.PriceListCode, "priceListCode");
        Compare(existingSnapshot.DeliveryType, currentSnapshot.DeliveryType, "deliveryType");
        Compare(existingSnapshot.CurrencyCode, currentSnapshot.CurrencyCode, "currencyCode");

        return changedFields;

        void Compare(string? oldValue, string? newValue, string fieldName)
        {
            if (!string.Equals(oldValue, newValue, StringComparison.Ordinal))
            {
                changedFields.Add(fieldName);
            }
        }
    }

    private static (int AddedLines, int RemovedLines, int ModifiedLines, int UnchangedLines, IReadOnlyList<PreviewLegacyOrderImportLineChange> LineChanges) BuildLineDiff(
        Domain.Entities.SalesOrder existingSnapshot,
        Domain.Entities.SalesOrder currentSnapshot)
    {
        var oldIndexed = IndexByMatchKey(existingSnapshot.Items);
        var newIndexed = IndexByMatchKey(currentSnapshot.Items);
        var allKeys = oldIndexed.Keys.Concat(newIndexed.Keys).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();

        var lineChanges = new List<PreviewLegacyOrderImportLineChange>();
        var addedLines = 0;
        var removedLines = 0;
        var modifiedLines = 0;
        var unchangedLines = 0;

        foreach (var key in allKeys)
        {
            oldIndexed.TryGetValue(key, out var oldLine);
            newIndexed.TryGetValue(key, out var newLine);

            if (oldLine is null && newLine is not null)
            {
                addedLines++;
                lineChanges.Add(new PreviewLegacyOrderImportLineChange
                {
                    ChangeType = PreviewLegacyOrderLineChangeType.Added,
                    MatchKey = key,
                    NewLine = MapLine(newLine)
                });
                continue;
            }

            if (oldLine is not null && newLine is null)
            {
                removedLines++;
                lineChanges.Add(new PreviewLegacyOrderImportLineChange
                {
                    ChangeType = PreviewLegacyOrderLineChangeType.Removed,
                    MatchKey = key,
                    OldLine = MapLine(oldLine)
                });
                continue;
            }

            var stableOldLine = oldLine!;
            var stableNewLine = newLine!;
            var changedFields = GetChangedFields(stableOldLine, stableNewLine);
            if (changedFields.Count == 0)
            {
                unchangedLines++;
                continue;
            }

            modifiedLines++;
            lineChanges.Add(new PreviewLegacyOrderImportLineChange
            {
                ChangeType = PreviewLegacyOrderLineChangeType.Modified,
                MatchKey = key,
                OldLine = MapLine(stableOldLine),
                NewLine = MapLine(stableNewLine),
                ChangedFields = changedFields
            });
        }

        return (addedLines, removedLines, modifiedLines, unchangedLines, lineChanges);
    }

    private static Dictionary<string, Domain.Entities.SalesOrderItem> IndexByMatchKey(IReadOnlyList<Domain.Entities.SalesOrderItem> items)
    {
        var counters = new Dictionary<string, int>(StringComparer.Ordinal);
        var indexed = new Dictionary<string, Domain.Entities.SalesOrderItem>(StringComparer.Ordinal);

        foreach (var item in items.OrderBy(x => x.LineNumber))
        {
            var baseKey = BuildBaseMatchKey(item);
            counters.TryGetValue(baseKey, out var currentCount);
            currentCount++;
            counters[baseKey] = currentCount;
            indexed[$"{baseKey}#{currentCount}"] = item;
        }

        return indexed;
    }

    private static string BuildBaseMatchKey(Domain.Entities.SalesOrderItem item)
    {
        var primaryToken = NormalizeToken(item.LegacyArticleId)
            ?? NormalizeToken(item.Sku)
            ?? NormalizeToken(item.Description)
            ?? "line";

        return primaryToken;
    }

    private static string? NormalizeToken(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToUpperInvariant();
    }

    private static List<string> GetChangedFields(Domain.Entities.SalesOrderItem oldLine, Domain.Entities.SalesOrderItem newLine)
    {
        var changedFields = new List<string>();
        CompareText(oldLine.Sku, newLine.Sku, "sku");
        CompareText(oldLine.Description, newLine.Description, "description");
        CompareText(oldLine.UnitCode, newLine.UnitCode, "unitCode");
        CompareText(oldLine.UnitName, newLine.UnitName, "unitName");
        CompareNumber(oldLine.Quantity, newLine.Quantity, "quantity");
        CompareNumber(oldLine.UnitPrice, newLine.UnitPrice, "unitPrice");
        CompareNumber(oldLine.DiscountAmount, newLine.DiscountAmount, "discountAmount");
        CompareNumber(oldLine.TaxAmount, newLine.TaxAmount, "taxAmount");
        CompareNumber(oldLine.LineTotal, newLine.LineTotal, "lineTotal");
        return changedFields;

        void CompareText(string? oldValue, string? newValue, string fieldName)
        {
            if (!string.Equals(oldValue, newValue, StringComparison.Ordinal))
            {
                changedFields.Add(fieldName);
            }
        }

        void CompareNumber(decimal oldValue, decimal newValue, string fieldName)
        {
            if (oldValue != newValue)
            {
                changedFields.Add(fieldName);
            }
        }
    }

    private static PreviewLegacyOrderLineSnapshot MapLine(Domain.Entities.SalesOrderItem item)
    {
        return new PreviewLegacyOrderLineSnapshot
        {
            LineNumber = item.LineNumber,
            LegacyArticleId = item.LegacyArticleId,
            Sku = item.Sku,
            Description = item.Description,
            UnitCode = item.UnitCode,
            UnitName = item.UnitName,
            Quantity = item.Quantity,
            UnitPrice = item.UnitPrice,
            DiscountAmount = item.DiscountAmount,
            TaxAmount = item.TaxAmount,
            LineTotal = item.LineTotal
        };
    }

}
