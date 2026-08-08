using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

public sealed class OrderDebtTraceReader : IOrderDebtTraceReader
{
    private const string LegacySourceSystem = "legacy";
    private const string LegacyOrdersSourceTable = "pedidos";

    private readonly BillingDbContext _dbContext;

    public OrderDebtTraceReader(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyDictionary<string, OrderDebtTraceSnapshot>> GetByLegacyOrderIdsAsync(
        IReadOnlyCollection<string> legacyOrderIds,
        CancellationToken cancellationToken = default)
    {
        var normalizedIds = legacyOrderIds
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedIds.Count == 0)
        {
            return new Dictionary<string, OrderDebtTraceSnapshot>(StringComparer.OrdinalIgnoreCase);
        }

        var requestedImportRecords = (await _dbContext.LegacyImportRecords
            .AsNoTracking()
            .Where(record =>
                record.SourceSystem == LegacySourceSystem
                && record.SourceTable == LegacyOrdersSourceTable
                && normalizedIds.Contains(record.SourceDocumentId))
            .ToListAsync(cancellationToken))
            .ToArray();
        var requestedImportRecordIds = requestedImportRecords
            .Select(record => record.Id)
            .Distinct()
            .ToList();
        var requestedSalesOrders = requestedImportRecordIds.Count == 0
            ? []
            : (await _dbContext.SalesOrders
                .AsNoTracking()
                .Where(order => requestedImportRecordIds.Contains(order.LegacyImportRecordId))
                .ToListAsync(cancellationToken))
                .ToArray();
        var requestedSalesOrderIds = requestedSalesOrders
            .Select(order => order.Id)
            .Distinct()
            .ToList();

        var primaryBillingDocuments = requestedSalesOrderIds.Count == 0
            ? []
            : (await _dbContext.BillingDocuments
                .AsNoTracking()
                .Where(document => requestedSalesOrderIds.Contains(document.SalesOrderId))
                .ToListAsync(cancellationToken))
                .ToArray();
        var requestedItemLinks = requestedSalesOrderIds.Count == 0
            ? []
            : (await _dbContext.BillingDocumentItems
                .AsNoTracking()
                .Where(item => requestedSalesOrderIds.Contains(item.SalesOrderId))
                .ToListAsync(cancellationToken))
                .ToArray();

        var candidateBillingDocumentIds = requestedImportRecords
            .Where(record => record.BillingDocumentId.HasValue)
            .Select(record => record.BillingDocumentId!.Value)
            .Concat(primaryBillingDocuments.Select(document => document.Id))
            .Concat(requestedItemLinks.Select(item => item.BillingDocumentId))
            .Distinct()
            .ToList();
        var billingDocuments = candidateBillingDocumentIds.Count == 0
            ? []
            : (await _dbContext.BillingDocuments
                .AsNoTracking()
                .Where(document => candidateBillingDocumentIds.Contains(document.Id))
                .ToListAsync(cancellationToken))
                .ToArray();
        var billingDocumentItems = candidateBillingDocumentIds.Count == 0
            ? []
            : (await _dbContext.BillingDocumentItems
                .AsNoTracking()
                .Where(item => candidateBillingDocumentIds.Contains(item.BillingDocumentId))
                .ToListAsync(cancellationToken))
                .ToArray();
        var fiscalDocuments = candidateBillingDocumentIds.Count == 0
            ? []
            : (await _dbContext.FiscalDocuments
                .AsNoTracking()
                .Where(document => candidateBillingDocumentIds.Contains(document.BillingDocumentId))
                .ToListAsync(cancellationToken))
                .ToArray();
        var fiscalDocumentIds = fiscalDocuments
            .Select(document => document.Id)
            .Distinct()
            .ToList();
        var fiscalStamps = fiscalDocumentIds.Count == 0
            ? []
            : (await _dbContext.FiscalStamps
                .AsNoTracking()
                .Where(stamp => fiscalDocumentIds.Contains(stamp.FiscalDocumentId))
                .ToListAsync(cancellationToken))
                .ToArray();
        var receivableInvoices = fiscalDocumentIds.Count == 0
            ? []
            : (await _dbContext.AccountsReceivableInvoices
                .AsNoTracking()
                .Where(invoice => invoice.FiscalDocumentId.HasValue && fiscalDocumentIds.Contains(invoice.FiscalDocumentId.Value))
                .ToListAsync(cancellationToken))
                .ToArray();

        var allAssociatedSalesOrderIds = billingDocuments
            .Select(document => document.SalesOrderId)
            .Concat(billingDocumentItems.Select(item => item.SalesOrderId))
            .Concat(requestedSalesOrderIds)
            .Distinct()
            .ToList();
        var allSalesOrders = allAssociatedSalesOrderIds.Count == 0
            ? []
            : (await _dbContext.SalesOrders
                .AsNoTracking()
                .Where(order => allAssociatedSalesOrderIds.Contains(order.Id))
                .ToListAsync(cancellationToken))
                .ToArray();
        var allImportRecordIds = allSalesOrders
            .Select(order => order.LegacyImportRecordId)
            .Distinct()
            .ToList();
        var allImportRecords = allImportRecordIds.Count == 0
            ? []
            : (await _dbContext.LegacyImportRecords
                .AsNoTracking()
                .Where(record => allImportRecordIds.Contains(record.Id))
                .ToListAsync(cancellationToken))
                .ToArray();

        var requestedImportsBySourceId = requestedImportRecords
            .GroupBy(record => record.SourceDocumentId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(record => record.Id).First(), StringComparer.OrdinalIgnoreCase);
        var requestedSalesOrdersByImportId = requestedSalesOrders
            .GroupBy(order => order.LegacyImportRecordId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(order => order.Id).First());
        var allSalesOrdersById = allSalesOrders.ToDictionary(order => order.Id);
        var allImportRecordsById = allImportRecords.ToDictionary(record => record.Id);

        var result = new Dictionary<string, OrderDebtTraceSnapshot>(StringComparer.OrdinalIgnoreCase);
        foreach (var legacyOrderId in normalizedIds)
        {
            if (!requestedImportsBySourceId.TryGetValue(legacyOrderId, out var importRecord))
            {
                result[legacyOrderId] = new OrderDebtTraceSnapshot
                {
                    LegacyOrderId = legacyOrderId
                };
                continue;
            }

            requestedSalesOrdersByImportId.TryGetValue(importRecord.Id, out var salesOrder);
            if (salesOrder is null)
            {
                result[legacyOrderId] = new OrderDebtTraceSnapshot
                {
                    LegacyOrderId = legacyOrderId,
                    LegacyImportRecordId = importRecord.Id,
                    BillingDocumentId = importRecord.BillingDocumentId,
                    HasUnconfirmedDirectLink = importRecord.BillingDocumentId.HasValue
                };
                continue;
            }

            var primaryDocumentIds = billingDocuments
                .Where(document => document.SalesOrderId == salesOrder.Id)
                .Select(document => document.Id)
                .ToHashSet();
            var itemDocumentIds = billingDocumentItems
                .Where(item => item.SalesOrderId == salesOrder.Id)
                .Select(item => item.BillingDocumentId)
                .ToHashSet();
            var confirmedDocumentIds = primaryDocumentIds
                .Concat(itemDocumentIds)
                .ToHashSet();
            var confirmedDocuments = billingDocuments
                .Where(document => confirmedDocumentIds.Contains(document.Id))
                .ToArray();
            var operationalConfirmedDocuments = confirmedDocuments
                .Where(document => IsOperational(document, fiscalDocuments))
                .OrderByDescending(document => document.Id)
                .ToArray();
            var directDocument = importRecord.BillingDocumentId.HasValue
                ? billingDocuments.FirstOrDefault(document => document.Id == importRecord.BillingDocumentId.Value)
                : null;
            var selectedBillingDocument = operationalConfirmedDocuments.FirstOrDefault()
                ?? confirmedDocuments.OrderByDescending(document => document.Id).FirstOrDefault()
                ?? directDocument;
            var membershipConfirmed = selectedBillingDocument is not null
                && confirmedDocumentIds.Contains(selectedBillingDocument.Id);
            var hasDirectLinkMismatch = importRecord.BillingDocumentId.HasValue
                && confirmedDocumentIds.Count > 0
                && !confirmedDocumentIds.Contains(importRecord.BillingDocumentId.Value);
            var selectedFiscalDocument = selectedBillingDocument is null
                ? null
                : SelectFiscalDocument(selectedBillingDocument.Id, fiscalDocuments);
            var selectedStamp = selectedFiscalDocument is null
                ? null
                : fiscalStamps
                    .Where(stamp =>
                        stamp.FiscalDocumentId == selectedFiscalDocument.Id
                        && stamp.Status == FiscalStampStatus.Succeeded
                        && !string.IsNullOrWhiteSpace(stamp.Uuid))
                    .OrderByDescending(stamp => stamp.StampedAtUtc ?? stamp.CreatedAtUtc)
                    .ThenByDescending(stamp => stamp.Id)
                    .FirstOrDefault();
            var receivableInvoice = selectedFiscalDocument is null
                ? null
                : receivableInvoices
                    .Where(invoice => invoice.FiscalDocumentId == selectedFiscalDocument.Id)
                    .OrderByDescending(invoice => invoice.Id)
                    .FirstOrDefault();

            result[legacyOrderId] = new OrderDebtTraceSnapshot
            {
                LegacyOrderId = legacyOrderId,
                LegacyImportRecordId = importRecord.Id,
                SalesOrderId = salesOrder.Id,
                BillingDocumentId = selectedBillingDocument?.Id ?? importRecord.BillingDocumentId,
                BillingDocumentStatus = selectedBillingDocument?.Status,
                MembershipConfirmed = membershipConfirmed,
                MembershipEvidence = BuildMembershipEvidence(selectedBillingDocument, salesOrder.Id, primaryDocumentIds, itemDocumentIds),
                HasUnconfirmedDirectLink = importRecord.BillingDocumentId.HasValue
                    && !confirmedDocumentIds.Contains(importRecord.BillingDocumentId.Value),
                HasDirectLinkMismatch = hasDirectLinkMismatch,
                ConfirmedOperationalDocumentCount = operationalConfirmedDocuments.Length,
                RelatedLegacyOrderIds = BuildRelatedLegacyOrderIds(
                    selectedBillingDocument,
                    billingDocumentItems,
                    allSalesOrdersById,
                    allImportRecordsById,
                    legacyOrderId),
                FiscalDocumentId = selectedFiscalDocument?.Id,
                FiscalDocumentStatus = selectedFiscalDocument?.Status,
                FiscalSeries = selectedFiscalDocument?.Series,
                FiscalFolio = selectedFiscalDocument?.Folio,
                FiscalUuid = selectedStamp?.Uuid,
                FiscalIssuedAtUtc = selectedFiscalDocument?.IssuedAtUtc,
                FiscalCurrencyCode = selectedFiscalDocument?.CurrencyCode,
                FiscalTotal = selectedFiscalDocument?.Total,
                PaymentMethodSat = selectedFiscalDocument?.PaymentMethodSat,
                PaymentFormSat = selectedFiscalDocument?.PaymentFormSat,
                IsCreditSale = selectedFiscalDocument?.IsCreditSale,
                AccountsReceivableInvoiceId = receivableInvoice?.Id,
                AccountsReceivableStatus = receivableInvoice?.Status,
                ReceivableCurrencyCode = receivableInvoice?.CurrencyCode,
                InvoiceTotal = receivableInvoice?.Total,
                PaidTotal = receivableInvoice?.PaidTotal,
                OutstandingBalance = receivableInvoice?.OutstandingBalance
            };
        }

        return result;
    }

    private static bool IsOperational(
        BillingDocument billingDocument,
        IReadOnlyCollection<FiscalDocument> fiscalDocuments)
    {
        if (billingDocument.Status == BillingDocumentStatus.Cancelled)
        {
            return false;
        }

        var fiscalDocument = SelectFiscalDocument(billingDocument.Id, fiscalDocuments);
        return fiscalDocument?.Status != FiscalDocumentStatus.Cancelled;
    }

    private static FiscalDocument? SelectFiscalDocument(
        long billingDocumentId,
        IReadOnlyCollection<FiscalDocument> fiscalDocuments)
    {
        return fiscalDocuments
            .Where(document =>
                document.BillingDocumentId == billingDocumentId
                && document.Status != FiscalDocumentStatus.DiscardedUnstamped)
            .OrderByDescending(document => document.Id)
            .FirstOrDefault();
    }

    private static string? BuildMembershipEvidence(
        BillingDocument? selectedBillingDocument,
        long salesOrderId,
        IReadOnlySet<long> primaryDocumentIds,
        IReadOnlySet<long> itemDocumentIds)
    {
        if (selectedBillingDocument is null)
        {
            return null;
        }

        var primary = primaryDocumentIds.Contains(selectedBillingDocument.Id);
        var items = itemDocumentIds.Contains(selectedBillingDocument.Id);
        return (primary, items) switch
        {
            (true, true) => "PrimarySalesOrderAndItems",
            (true, false) => "PrimarySalesOrder",
            (false, true) => "BillingDocumentItems",
            _ => null
        };
    }

    private static IReadOnlyList<string> BuildRelatedLegacyOrderIds(
        BillingDocument? selectedBillingDocument,
        IReadOnlyCollection<BillingDocumentItem> billingDocumentItems,
        IReadOnlyDictionary<long, SalesOrder> salesOrdersById,
        IReadOnlyDictionary<long, LegacyImportRecord> importRecordsById,
        string requestedLegacyOrderId)
    {
        if (selectedBillingDocument is null)
        {
            return [requestedLegacyOrderId];
        }

        var salesOrderIds = billingDocumentItems
            .Where(item => item.BillingDocumentId == selectedBillingDocument.Id)
            .Select(item => item.SalesOrderId)
            .Append(selectedBillingDocument.SalesOrderId)
            .Distinct()
            .ToArray();
        var legacyOrderIds = salesOrderIds
            .Select(salesOrderId => salesOrdersById.TryGetValue(salesOrderId, out var salesOrder) ? salesOrder : null)
            .Where(salesOrder => salesOrder is not null)
            .Select(salesOrder => importRecordsById.TryGetValue(salesOrder!.LegacyImportRecordId, out var importRecord) ? importRecord.SourceDocumentId : null)
            .Where(sourceDocumentId => !string.IsNullOrWhiteSpace(sourceDocumentId))
            .Cast<string>()
            .Append(requestedLegacyOrderId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return legacyOrderIds;
    }
}
