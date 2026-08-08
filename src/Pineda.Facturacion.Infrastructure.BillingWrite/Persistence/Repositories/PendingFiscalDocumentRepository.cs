using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.UseCases.FiscalDocuments;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

public sealed class PendingFiscalDocumentRepository : IPendingFiscalDocumentRepository
{
    private const int MaxVisibleOrderReferences = 5;

    private readonly BillingDbContext _dbContext;

    public PendingFiscalDocumentRepository(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SearchPendingFiscalDocumentsResult> SearchAsync(
        SearchPendingFiscalDocumentsFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var candidates =
            from billingDocument in _dbContext.BillingDocuments.AsNoTracking()
            join primarySalesOrder in _dbContext.SalesOrders.AsNoTracking()
                on billingDocument.SalesOrderId equals primarySalesOrder.Id
            join primaryImportRecord in _dbContext.LegacyImportRecords.AsNoTracking()
                on primarySalesOrder.LegacyImportRecordId equals primaryImportRecord.Id
            join fiscalDocument in _dbContext.FiscalDocuments.AsNoTracking()
                on billingDocument.Id equals fiscalDocument.BillingDocumentId into fiscalDocumentGroup
            from fiscalDocument in fiscalDocumentGroup.DefaultIfEmpty()
            where billingDocument.Status == BillingDocumentStatus.Draft
                && (fiscalDocument == null
                    || fiscalDocument.Status == FiscalDocumentStatus.Draft
                    || fiscalDocument.Status == FiscalDocumentStatus.ReadyForStamping
                    || fiscalDocument.Status == FiscalDocumentStatus.StampingRejected
                    || fiscalDocument.Status == FiscalDocumentStatus.DiscardedUnstamped)
            select new PendingFiscalDocumentBaseRow
            {
                BillingDocumentId = billingDocument.Id,
                PrimarySalesOrderId = billingDocument.SalesOrderId,
                PrimarySourceDocumentId = primaryImportRecord.SourceDocumentId,
                PrimaryLegacyOrderNumber = primarySalesOrder.LegacyOrderNumber,
                BillingDocumentStatus = billingDocument.Status,
                FiscalDocumentId = fiscalDocument != null ? fiscalDocument.Id : null,
                FiscalDocumentStatus = fiscalDocument != null ? fiscalDocument.Status : null,
                DocumentType = billingDocument.DocumentType,
                Series = fiscalDocument != null ? fiscalDocument.Series : billingDocument.Series,
                Folio = fiscalDocument != null ? fiscalDocument.Folio : billingDocument.Folio,
                ReceiverName = fiscalDocument != null
                    ? fiscalDocument.ReceiverLegalName
                    : primarySalesOrder.CustomerName,
                ReceiverRfc = fiscalDocument != null
                    ? fiscalDocument.ReceiverRfc
                    : primarySalesOrder.CustomerRfc,
                CurrencyCode = billingDocument.CurrencyCode,
                Total = billingDocument.Total,
                CreatedAtUtc = billingDocument.CreatedAtUtc,
                LastActivityAtUtc = fiscalDocument != null && fiscalDocument.UpdatedAtUtc > billingDocument.UpdatedAtUtc
                    ? fiscalDocument.UpdatedAtUtc
                    : billingDocument.UpdatedAtUtc,
                FiscalPreparedAtUtc = fiscalDocument != null ? fiscalDocument.CreatedAtUtc : null,
                PaymentMethodSat = fiscalDocument != null
                    ? fiscalDocument.PaymentMethodSat
                    : billingDocument.PaymentMethodSat,
                PaymentFormSat = fiscalDocument != null
                    ? fiscalDocument.PaymentFormSat
                    : billingDocument.PaymentFormSat
            };

        candidates = ApplyWorkFilter(candidates, filter.WorkFilter);
        candidates = ApplySearch(candidates, filter.Query);

        var totalCount = await candidates.CountAsync(cancellationToken);
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 25 : filter.PageSize;
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        var orderedCandidates = ApplySort(candidates, filter.Sort);
        var pageRows = await orderedCandidates
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        if (pageRows.Count == 0)
        {
            return new SearchPendingFiscalDocumentsResult
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
                Items = []
            };
        }

        var pageBillingDocumentIds = pageRows
            .Select(row => row.BillingDocumentId)
            .Distinct()
            .ToArray();

        var itemOrderLinks = await (
                from item in _dbContext.BillingDocumentItems.AsNoTracking()
                join salesOrder in _dbContext.SalesOrders.AsNoTracking()
                    on item.SalesOrderId equals salesOrder.Id
                join importRecord in _dbContext.LegacyImportRecords.AsNoTracking()
                    on salesOrder.LegacyImportRecordId equals importRecord.Id
                where pageBillingDocumentIds.Contains(item.BillingDocumentId)
                select new PendingFiscalDocumentItemOrderLink
                {
                    BillingDocumentId = item.BillingDocumentId,
                    SalesOrderId = salesOrder.Id,
                    SourceDocumentId = importRecord.SourceDocumentId,
                    LegacyOrderNumber = salesOrder.LegacyOrderNumber
                })
            .ToListAsync(cancellationToken);

        var itemLinksByDocument = itemOrderLinks
            .GroupBy(link => link.BillingDocumentId)
            .ToDictionary(group => group.Key, group => group.ToArray());

        var items = pageRows
            .Select(row => MapItem(
                row,
                itemLinksByDocument.TryGetValue(row.BillingDocumentId, out var links)
                    ? links
                    : []))
            .ToArray();

        return new SearchPendingFiscalDocumentsResult
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            Items = items
        };
    }

    private IQueryable<PendingFiscalDocumentBaseRow> ApplySearch(
        IQueryable<PendingFiscalDocumentBaseRow> candidates,
        string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return candidates;
        }

        var search = query.Trim();
        var parsedNumeric = long.TryParse(search, out var numericValue);

        var associatedOrderMatches =
            from item in _dbContext.BillingDocumentItems.AsNoTracking()
            join salesOrder in _dbContext.SalesOrders.AsNoTracking()
                on item.SalesOrderId equals salesOrder.Id
            join importRecord in _dbContext.LegacyImportRecords.AsNoTracking()
                on salesOrder.LegacyImportRecordId equals importRecord.Id
            where (parsedNumeric && salesOrder.Id == numericValue)
                || importRecord.SourceDocumentId.Contains(search)
                || salesOrder.LegacyOrderNumber.Contains(search)
                || salesOrder.CustomerName.Contains(search)
                || (salesOrder.CustomerRfc != null && salesOrder.CustomerRfc.Contains(search))
            select item.BillingDocumentId;

        return candidates.Where(row =>
            (parsedNumeric
                && (row.BillingDocumentId == numericValue
                    || row.FiscalDocumentId == numericValue
                    || row.PrimarySalesOrderId == numericValue))
            || row.PrimarySourceDocumentId.Contains(search)
            || row.PrimaryLegacyOrderNumber.Contains(search)
            || row.ReceiverName.Contains(search)
            || (row.ReceiverRfc != null && row.ReceiverRfc.Contains(search))
            || (row.Series != null && row.Series.Contains(search))
            || (row.Folio != null && row.Folio.Contains(search))
            || associatedOrderMatches.Contains(row.BillingDocumentId));
    }

    private static IQueryable<PendingFiscalDocumentBaseRow> ApplyWorkFilter(
        IQueryable<PendingFiscalDocumentBaseRow> candidates,
        PendingFiscalDocumentWorkFilter workFilter)
    {
        return workFilter switch
        {
            PendingFiscalDocumentWorkFilter.PendingPreparation => candidates.Where(row =>
                !row.FiscalDocumentId.HasValue
                || row.FiscalDocumentStatus == FiscalDocumentStatus.Draft),
            PendingFiscalDocumentWorkFilter.ReadyForStamping => candidates.Where(row =>
                row.FiscalDocumentStatus == FiscalDocumentStatus.ReadyForStamping),
            PendingFiscalDocumentWorkFilter.RequiresAttention => candidates.Where(row =>
                row.FiscalDocumentStatus == FiscalDocumentStatus.StampingRejected
                || row.FiscalDocumentStatus == FiscalDocumentStatus.DiscardedUnstamped),
            _ => candidates
        };
    }

    private static IOrderedQueryable<PendingFiscalDocumentBaseRow> ApplySort(
        IQueryable<PendingFiscalDocumentBaseRow> candidates,
        PendingFiscalDocumentSort sort)
    {
        return sort switch
        {
            PendingFiscalDocumentSort.OldestFirst => candidates
                .OrderBy(row => row.LastActivityAtUtc)
                .ThenBy(row => row.BillingDocumentId),
            PendingFiscalDocumentSort.TotalDesc => candidates
                .OrderByDescending(row => row.Total)
                .ThenByDescending(row => row.LastActivityAtUtc)
                .ThenByDescending(row => row.BillingDocumentId),
            _ => candidates
                .OrderByDescending(row => row.LastActivityAtUtc)
                .ThenByDescending(row => row.BillingDocumentId)
        };
    }

    private static PendingFiscalDocumentListItem MapItem(
        PendingFiscalDocumentBaseRow row,
        IReadOnlyCollection<PendingFiscalDocumentItemOrderLink> itemLinks)
    {
        var orderLinks = itemLinks
            .Append(new PendingFiscalDocumentItemOrderLink
            {
                BillingDocumentId = row.BillingDocumentId,
                SalesOrderId = row.PrimarySalesOrderId,
                SourceDocumentId = row.PrimarySourceDocumentId,
                LegacyOrderNumber = row.PrimaryLegacyOrderNumber
            })
            .GroupBy(link => link.SalesOrderId)
            .Select(group => group.First())
            .OrderBy(link => link.SalesOrderId == row.PrimarySalesOrderId ? 0 : 1)
            .ThenBy(link => link.SalesOrderId)
            .ToArray();

        var workStatus = ResolveWorkStatus(row.FiscalDocumentId, row.FiscalDocumentStatus);

        return new PendingFiscalDocumentListItem
        {
            BillingDocumentId = row.BillingDocumentId,
            FiscalDocumentId = row.FiscalDocumentId,
            BillingDocumentStatus = row.BillingDocumentStatus.ToString(),
            FiscalDocumentStatus = row.FiscalDocumentStatus?.ToString(),
            WorkStatus = workStatus,
            WorkStatusLabel = PendingFiscalDocumentWorkStatusLabels.GetLabel(workStatus),
            RequiresAttention = PendingFiscalDocumentWorkStatusLabels.RequiresAttention(workStatus),
            DocumentType = row.DocumentType,
            Series = row.Series,
            Folio = row.Folio,
            ReceiverName = row.ReceiverName,
            ReceiverRfc = row.ReceiverRfc,
            CurrencyCode = string.IsNullOrWhiteSpace(row.CurrencyCode)
                ? "MXN"
                : row.CurrencyCode.Trim().ToUpperInvariant(),
            Total = row.Total,
            AssociatedOrderCount = orderLinks.Length,
            ItemCount = itemLinks.Count,
            OrderReferences = orderLinks
                .Select(BuildOrderReference)
                .Where(reference => !string.IsNullOrWhiteSpace(reference))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(MaxVisibleOrderReferences)
                .ToArray(),
            CreatedAtUtc = row.CreatedAtUtc,
            LastActivityAtUtc = row.LastActivityAtUtc,
            FiscalPreparedAtUtc = row.FiscalPreparedAtUtc,
            PaymentMethodSat = row.PaymentMethodSat,
            PaymentFormSat = row.PaymentFormSat
        };
    }

    private static PendingFiscalDocumentWorkStatus ResolveWorkStatus(
        long? fiscalDocumentId,
        FiscalDocumentStatus? fiscalDocumentStatus)
    {
        if (!fiscalDocumentId.HasValue)
        {
            return PendingFiscalDocumentWorkStatus.PendingPreparation;
        }

        return fiscalDocumentStatus switch
        {
            FiscalDocumentStatus.Draft => PendingFiscalDocumentWorkStatus.InPreparation,
            FiscalDocumentStatus.ReadyForStamping => PendingFiscalDocumentWorkStatus.ReadyForStamping,
            FiscalDocumentStatus.StampingRejected => PendingFiscalDocumentWorkStatus.StampingRejected,
            FiscalDocumentStatus.DiscardedUnstamped => PendingFiscalDocumentWorkStatus.NeedsRegeneration,
            _ => PendingFiscalDocumentWorkStatus.InPreparation
        };
    }

    private static string BuildOrderReference(PendingFiscalDocumentItemOrderLink link)
    {
        var sourceDocumentId = link.SourceDocumentId?.Trim();
        var legacyOrderNumber = link.LegacyOrderNumber?.Trim();

        if (string.IsNullOrWhiteSpace(sourceDocumentId))
        {
            return legacyOrderNumber ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(legacyOrderNumber)
            || string.Equals(sourceDocumentId, legacyOrderNumber, StringComparison.OrdinalIgnoreCase))
        {
            return sourceDocumentId;
        }

        return $"{sourceDocumentId}-{legacyOrderNumber}";
    }

    private sealed class PendingFiscalDocumentBaseRow
    {
        public long BillingDocumentId { get; init; }

        public long PrimarySalesOrderId { get; init; }

        public string PrimarySourceDocumentId { get; init; } = string.Empty;

        public string PrimaryLegacyOrderNumber { get; init; } = string.Empty;

        public BillingDocumentStatus BillingDocumentStatus { get; init; }

        public long? FiscalDocumentId { get; init; }

        public FiscalDocumentStatus? FiscalDocumentStatus { get; init; }

        public string DocumentType { get; init; } = string.Empty;

        public string? Series { get; init; }

        public string? Folio { get; init; }

        public string ReceiverName { get; init; } = string.Empty;

        public string? ReceiverRfc { get; init; }

        public string CurrencyCode { get; init; } = string.Empty;

        public decimal Total { get; init; }

        public DateTime CreatedAtUtc { get; init; }

        public DateTime LastActivityAtUtc { get; init; }

        public DateTime? FiscalPreparedAtUtc { get; init; }

        public string? PaymentMethodSat { get; init; }

        public string? PaymentFormSat { get; init; }
    }

    private sealed class PendingFiscalDocumentItemOrderLink
    {
        public long BillingDocumentId { get; init; }

        public long SalesOrderId { get; init; }

        public string? SourceDocumentId { get; init; }

        public string? LegacyOrderNumber { get; init; }
    }
}
