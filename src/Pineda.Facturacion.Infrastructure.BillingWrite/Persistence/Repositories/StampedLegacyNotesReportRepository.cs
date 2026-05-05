using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.UseCases.Reports;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

public sealed class StampedLegacyNotesReportRepository : IStampedLegacyNotesReportRepository
{
    private readonly BillingDbContext _dbContext;

    public StampedLegacyNotesReportRepository(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SearchStampedLegacyNotesReportResult> SearchAsync(
        StampedLegacyNotesReportQuery query,
        CancellationToken cancellationToken = default)
    {
        var reportQuery = BuildQuery(query);
        var totalCount = await reportQuery.CountAsync(cancellationToken);
        var page = query.Page.GetValueOrDefault(1);
        var pageSize = query.PageSize.GetValueOrDefault(50);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        var rawItems = await reportQuery
            .OrderByDescending(x => x.StampedAtUtc)
            .ThenBy(x => x.LegacyOrderId)
            .ThenBy(x => x.FiscalDocumentId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new SearchStampedLegacyNotesReportResult
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            Items = rawItems.Select(Map).ToList()
        };
    }

    public async Task<IReadOnlyList<StampedLegacyNoteReportItem>> ListForExportAsync(
        StampedLegacyNotesReportQuery query,
        CancellationToken cancellationToken = default)
    {
        var rawItems = await BuildQuery(query)
            .OrderByDescending(x => x.StampedAtUtc)
            .ThenBy(x => x.LegacyOrderId)
            .ThenBy(x => x.FiscalDocumentId)
            .ToListAsync(cancellationToken);

        return rawItems.Select(Map).ToList();
    }

    private IQueryable<StampedLegacyNoteReportRawItem> BuildQuery(StampedLegacyNotesReportQuery query)
    {
        var joinedQuery = from fiscalDocument in _dbContext.FiscalDocuments.AsNoTracking()
                          join fiscalStamp in _dbContext.FiscalStamps.AsNoTracking()
                              on fiscalDocument.Id equals fiscalStamp.FiscalDocumentId
                          join billingDocument in _dbContext.BillingDocuments.AsNoTracking()
                              on fiscalDocument.BillingDocumentId equals billingDocument.Id
                          join billingDocumentItem in _dbContext.BillingDocumentItems.AsNoTracking()
                              on billingDocument.Id equals billingDocumentItem.BillingDocumentId
                          join salesOrder in _dbContext.SalesOrders.AsNoTracking()
                              on billingDocumentItem.SalesOrderId equals salesOrder.Id
                          join legacyImportRecord in _dbContext.LegacyImportRecords.AsNoTracking()
                              on salesOrder.LegacyImportRecordId equals legacyImportRecord.Id
                          join fiscalCancellation in _dbContext.FiscalCancellations.AsNoTracking()
                              on fiscalDocument.Id equals fiscalCancellation.FiscalDocumentId into cancellationGroup
                          from cancellation in cancellationGroup.DefaultIfEmpty()
                          where fiscalStamp.Status == FiscalStampStatus.Succeeded
                              && fiscalStamp.Uuid != null
                              && fiscalStamp.Uuid!.Trim() != string.Empty
                              && fiscalStamp.StampedAtUtc != null
                              && fiscalStamp.StampedAtUtc >= query.FromUtc
                              && fiscalStamp.StampedAtUtc < query.ToUtcExclusive
                              && (fiscalDocument.Status == FiscalDocumentStatus.Stamped
                                  || fiscalDocument.Status == FiscalDocumentStatus.CancellationRejected)
                              && (cancellation == null || cancellation.Status != FiscalCancellationStatus.Cancelled)
                          select new
                          {
                              FiscalDocument = fiscalDocument,
                              FiscalStamp = fiscalStamp,
                              BillingDocumentItem = billingDocumentItem,
                              SalesOrder = salesOrder,
                              LegacyImportRecord = legacyImportRecord,
                              FiscalCancellation = cancellation
                          };

        if (!string.IsNullOrWhiteSpace(query.ReceiverSearch))
        {
            var receiverSearch = query.ReceiverSearch.Trim();
            joinedQuery = joinedQuery.Where(x =>
                x.FiscalDocument.ReceiverRfc.Contains(receiverSearch)
                || x.FiscalDocument.ReceiverLegalName.Contains(receiverSearch));
        }

        if (!string.IsNullOrWhiteSpace(query.Uuid))
        {
            var uuid = query.Uuid.Trim();
            joinedQuery = joinedQuery.Where(x => x.FiscalStamp.Uuid != null && x.FiscalStamp.Uuid.Contains(uuid));
        }

        if (!string.IsNullOrWhiteSpace(query.Series))
        {
            var series = query.Series.Trim();
            joinedQuery = joinedQuery.Where(x => x.FiscalDocument.Series != null && x.FiscalDocument.Series.Contains(series));
        }

        if (!string.IsNullOrWhiteSpace(query.Folio))
        {
            var folio = query.Folio.Trim();
            joinedQuery = joinedQuery.Where(x => x.FiscalDocument.Folio != null && x.FiscalDocument.Folio.Contains(folio));
        }

        if (!string.IsNullOrWhiteSpace(query.LegacyOrderId))
        {
            var legacyOrderId = query.LegacyOrderId.Trim();
            joinedQuery = joinedQuery.Where(x => x.LegacyImportRecord.SourceDocumentId.Contains(legacyOrderId));
        }

        if (!string.IsNullOrWhiteSpace(query.LegacyOrderNumber))
        {
            var legacyOrderNumber = query.LegacyOrderNumber.Trim();
            joinedQuery = joinedQuery.Where(x => x.SalesOrder.LegacyOrderNumber.Contains(legacyOrderNumber));
        }

        return from row in joinedQuery
               group row by new
               {
                   row.FiscalDocument.Id,
                   row.FiscalDocument.BillingDocumentId,
                   row.FiscalDocument.Series,
                   row.FiscalDocument.Folio,
                   row.FiscalDocument.Status,
                   row.FiscalDocument.ReceiverRfc,
                   row.FiscalDocument.ReceiverLegalName,
                   row.FiscalDocument.Total,
                   row.FiscalDocument.CurrencyCode,
                   row.FiscalStamp.Uuid,
                   row.FiscalStamp.StampedAtUtc,
                   LegacyOrderId = row.LegacyImportRecord.SourceDocumentId,
                   row.SalesOrder.LegacyOrderNumber,
                   CancellationStatus = row.FiscalCancellation == null
                       ? null
                       : (FiscalCancellationStatus?)row.FiscalCancellation.Status
               }
            into grouped
               select new StampedLegacyNoteReportRawItem
               {
                   FiscalDocumentId = grouped.Key.Id,
                   BillingDocumentId = grouped.Key.BillingDocumentId,
                   Series = grouped.Key.Series,
                   Folio = grouped.Key.Folio,
                   FiscalStatus = grouped.Key.Status,
                   CancellationStatus = grouped.Key.CancellationStatus,
                   ReceiverRfc = grouped.Key.ReceiverRfc,
                   ReceiverName = grouped.Key.ReceiverLegalName,
                   CfdiTotal = grouped.Key.Total,
                   CurrencyCode = grouped.Key.CurrencyCode,
                   Uuid = grouped.Key.Uuid!,
                   StampedAtUtc = grouped.Key.StampedAtUtc!.Value,
                   LegacyOrderId = grouped.Key.LegacyOrderId,
                   LegacyOrderNumber = grouped.Key.LegacyOrderNumber,
                   NoteAmountInCfdi = grouped.Sum(x => x.BillingDocumentItem.LineTotal + x.BillingDocumentItem.TaxAmount),
                   ItemCount = grouped.Count()
               };
    }

    private static StampedLegacyNoteReportItem Map(StampedLegacyNoteReportRawItem item)
    {
        return new StampedLegacyNoteReportItem
        {
            StampedAtUtc = item.StampedAtUtc,
            StampedAtLocalText = MexicoLocalDateRangeConverter.FormatStampedAtLocal(item.StampedAtUtc),
            LegacyOrderId = item.LegacyOrderId,
            LegacyOrderNumber = item.LegacyOrderNumber,
            BillingDocumentId = item.BillingDocumentId,
            FiscalDocumentId = item.FiscalDocumentId,
            Series = item.Series,
            Folio = item.Folio,
            Uuid = item.Uuid,
            FiscalStatus = item.FiscalStatus.ToString(),
            CancellationStatus = item.CancellationStatus?.ToString(),
            ReceiverName = item.ReceiverName,
            ReceiverRfc = item.ReceiverRfc,
            CfdiTotal = item.CfdiTotal,
            NoteAmountInCfdi = item.NoteAmountInCfdi,
            CurrencyCode = item.CurrencyCode,
            ItemCount = item.ItemCount
        };
    }

    private sealed class StampedLegacyNoteReportRawItem
    {
        public DateTime StampedAtUtc { get; init; }
        public string LegacyOrderId { get; init; } = string.Empty;
        public string? LegacyOrderNumber { get; init; }
        public long BillingDocumentId { get; init; }
        public long FiscalDocumentId { get; init; }
        public string? Series { get; init; }
        public string? Folio { get; init; }
        public string Uuid { get; init; } = string.Empty;
        public FiscalDocumentStatus FiscalStatus { get; init; }
        public FiscalCancellationStatus? CancellationStatus { get; init; }
        public string ReceiverName { get; init; } = string.Empty;
        public string ReceiverRfc { get; init; } = string.Empty;
        public decimal CfdiTotal { get; init; }
        public decimal NoteAmountInCfdi { get; init; }
        public string CurrencyCode { get; init; } = string.Empty;
        public int ItemCount { get; init; }
    }
}
