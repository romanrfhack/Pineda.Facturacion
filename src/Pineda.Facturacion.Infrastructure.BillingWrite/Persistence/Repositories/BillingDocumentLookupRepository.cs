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

    public async Task<BillingDocumentLookupModel?> GetByIdAsync(long billingDocumentId, CancellationToken cancellationToken = default)
    {
        var billingDocument = await _dbContext.BillingDocuments
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
                        BillingDocumentItemId = item.Id,
                        SalesOrderId = item.SalesOrderId,
                        SalesOrderItemId = item.SalesOrderItemId,
                        SourceBillingDocumentItemRemovalId = item.SourceBillingDocumentItemRemovalId,
                        SourceSalesOrderLineNumber = item.SourceSalesOrderLineNumber,
                        SourceLegacyOrderId = item.SourceLegacyOrderId,
                        LineNumber = item.LineNumber,
                        ProductInternalCode = item.ProductInternalCode,
                        Description = item.Description,
                        Quantity = item.Quantity,
                        Total = item.LineTotal + item.TaxAmount
                    })
                    .ToList(),
                AssociatedOrders =
                    (from salesOrder in _dbContext.SalesOrders
                     join legacyImportRecord in _dbContext.LegacyImportRecords
                         on salesOrder.LegacyImportRecordId equals legacyImportRecord.Id
                     where legacyImportRecord.BillingDocumentId == billingDocument.Id
                         || salesOrder.Id == billingDocument.SalesOrderId
                     orderby salesOrder.Id == billingDocument.SalesOrderId ? 0 : 1, salesOrder.Id
                     select new BillingDocumentAssociatedOrderLookupModel
                     {
                         SalesOrderId = salesOrder.Id,
                         LegacyOrderId = string.IsNullOrEmpty(legacyImportRecord.SourceDocumentId)
                             ? salesOrder.LegacyOrderNumber
                             : string.IsNullOrEmpty(salesOrder.LegacyOrderNumber)
                                 ? legacyImportRecord.SourceDocumentId
                                 : legacyImportRecord.SourceDocumentId + "-" + salesOrder.LegacyOrderNumber,
                         CustomerName = salesOrder.CustomerName,
                         Total = salesOrder.Total,
                         IsPrimary = salesOrder.Id == billingDocument.SalesOrderId
                     })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (billingDocument is null)
        {
            return null;
        }

        var removedItems = await BuildRemovedItemTraceAsync(billingDocumentId, cancellationToken);
        return new BillingDocumentLookupModel
        {
            BillingDocumentId = billingDocument.BillingDocumentId,
            SalesOrderId = billingDocument.SalesOrderId,
            LegacyOrderId = billingDocument.LegacyOrderId,
            Status = billingDocument.Status,
            DocumentType = billingDocument.DocumentType,
            CurrencyCode = billingDocument.CurrencyCode,
            Total = billingDocument.Total,
            CreatedAtUtc = billingDocument.CreatedAtUtc,
            FiscalDocumentId = billingDocument.FiscalDocumentId,
            FiscalDocumentStatus = billingDocument.FiscalDocumentStatus,
            Items = billingDocument.Items,
            AssociatedOrders = billingDocument.AssociatedOrders,
            RemovedItems = removedItems
        };
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
                FiscalDocumentStatus = fiscalDocument != null ? fiscalDocument.Status.ToString() : null,
                AssociatedOrders =
                    (from linkedSalesOrder in _dbContext.SalesOrders.AsNoTracking()
                     join linkedImportRecord in _dbContext.LegacyImportRecords.AsNoTracking()
                         on linkedSalesOrder.LegacyImportRecordId equals linkedImportRecord.Id
                     where linkedImportRecord.BillingDocumentId == billingDocument.Id
                         || linkedSalesOrder.Id == billingDocument.SalesOrderId
                     orderby linkedSalesOrder.Id == billingDocument.SalesOrderId ? 0 : 1, linkedSalesOrder.Id
                     select new BillingDocumentAssociatedOrderLookupModel
                     {
                         SalesOrderId = linkedSalesOrder.Id,
                         LegacyOrderId = string.IsNullOrEmpty(linkedImportRecord.SourceDocumentId)
                             ? linkedSalesOrder.LegacyOrderNumber
                             : string.IsNullOrEmpty(linkedSalesOrder.LegacyOrderNumber)
                                 ? linkedImportRecord.SourceDocumentId
                                 : linkedImportRecord.SourceDocumentId + "-" + linkedSalesOrder.LegacyOrderNumber,
                         CustomerName = linkedSalesOrder.CustomerName,
                         Total = linkedSalesOrder.Total,
                         IsPrimary = linkedSalesOrder.Id == billingDocument.SalesOrderId
                     })
                    .ToList()
            };
    }

    private async Task<IReadOnlyList<BillingDocumentRemovedItemTraceModel>> BuildRemovedItemTraceAsync(
        long billingDocumentId,
        CancellationToken cancellationToken)
    {
        var removals = await (
                from removal in _dbContext.BillingDocumentItemRemovals.AsNoTracking()
                join salesOrder in _dbContext.SalesOrders.AsNoTracking() on removal.SalesOrderId equals salesOrder.Id
                where removal.BillingDocumentId == billingDocumentId
                orderby removal.RemovedAtUtc descending, removal.Id descending
                select new
                {
                    Removal = removal,
                    salesOrder.CustomerName
                })
            .ToListAsync(cancellationToken);

        if (removals.Count == 0)
        {
            return [];
        }

        var removalIds = removals.Select(x => x.Removal.Id).ToList();
        var assignments = await _dbContext.BillingDocumentPendingItemAssignments
            .AsNoTracking()
            .Where(x => removalIds.Contains(x.BillingDocumentItemRemovalId))
            .OrderBy(x => x.AssignedAtUtc)
            .ToListAsync(cancellationToken);

        var destinationBillingDocumentIds = assignments.Select(x => x.DestinationBillingDocumentId).Distinct().ToList();
        var destinationBillingDocuments = destinationBillingDocumentIds.Count == 0
            ? new List<DestinationBillingDocumentProjection>()
            : await _dbContext.BillingDocuments
                .AsNoTracking()
                .Where(x => destinationBillingDocumentIds.Contains(x.Id))
                .Select(x => new DestinationBillingDocumentProjection
                {
                    Id = x.Id,
                    Status = x.Status.ToString()
                })
                .ToListAsync(cancellationToken);

        var destinationFiscalDocuments = destinationBillingDocumentIds.Count == 0
            ? new List<DestinationFiscalDocumentProjection>()
            : await _dbContext.FiscalDocuments
                .AsNoTracking()
                .Where(x => destinationBillingDocumentIds.Contains(x.BillingDocumentId))
                .Select(x => new DestinationFiscalDocumentProjection
                {
                    Id = x.Id,
                    BillingDocumentId = x.BillingDocumentId,
                    Status = x.Status.ToString(),
                    Series = x.Series,
                    Folio = x.Folio
                })
                .ToListAsync(cancellationToken);

        var destinationFiscalDocumentIds = destinationFiscalDocuments.Select(x => x.Id).Distinct().ToList();
        var destinationStamps = destinationFiscalDocumentIds.Count == 0
            ? new List<DestinationStampProjection>()
            : await _dbContext.FiscalStamps
                .AsNoTracking()
                .Where(x => destinationFiscalDocumentIds.Contains(x.FiscalDocumentId))
                .Select(x => new DestinationStampProjection
                {
                    FiscalDocumentId = x.FiscalDocumentId,
                    Uuid = x.Uuid,
                    StampedAtUtc = x.StampedAtUtc
                })
                .ToListAsync(cancellationToken);

        var destinationBillingDocumentMap = destinationBillingDocuments.ToDictionary(x => x.Id);
        var destinationFiscalDocumentMap = destinationFiscalDocuments.ToDictionary(x => x.BillingDocumentId);
        var destinationStampMap = destinationStamps.ToDictionary(x => x.FiscalDocumentId);
        var assignmentsByRemoval = assignments
            .GroupBy(x => x.BillingDocumentItemRemovalId)
            .ToDictionary(x => x.Key, x => x.OrderBy(y => y.AssignedAtUtc).ToArray());

        var result = new List<BillingDocumentRemovedItemTraceModel>(removals.Count);
        foreach (var entry in removals)
        {
            assignmentsByRemoval.TryGetValue(entry.Removal.Id, out var assignmentHistory);
            assignmentHistory ??= Array.Empty<Domain.Entities.BillingDocumentPendingItemAssignment>();

            var activeAssignment = assignmentHistory.LastOrDefault(x => x.ReleasedAtUtc is null);
            var currentDestinationBilling = activeAssignment is null || !destinationBillingDocumentMap.TryGetValue(activeAssignment.DestinationBillingDocumentId, out var currentBillingDocument)
                ? null
                : currentBillingDocument;
            var currentDestinationFiscal = activeAssignment is null || !destinationFiscalDocumentMap.TryGetValue(activeAssignment.DestinationBillingDocumentId, out var currentFiscalDocument)
                ? null
                : currentFiscalDocument;
            var currentDestinationStamp = currentDestinationFiscal is null || !destinationStampMap.TryGetValue(currentDestinationFiscal.Id, out var currentStamp)
                ? null
                : currentStamp;

            var assignmentTrace = assignmentHistory.Select(assignment =>
            {
                var destinationFiscal = destinationFiscalDocumentMap.TryGetValue(assignment.DestinationBillingDocumentId, out var destinationFiscalDocument)
                    ? destinationFiscalDocument
                    : null;
                var destinationStamp = destinationFiscal is null || !destinationStampMap.TryGetValue(destinationFiscal.Id, out var stamp)
                    ? null
                    : stamp;

                return new BillingDocumentRemovedItemAssignmentTraceModel
                {
                    AssignmentId = assignment.Id,
                    DestinationBillingDocumentId = assignment.DestinationBillingDocumentId,
                    DestinationBillingDocumentStatus = destinationBillingDocumentMap.TryGetValue(assignment.DestinationBillingDocumentId, out var destinationBillingDocument)
                        ? destinationBillingDocument.Status
                        : null,
                    DestinationFiscalDocumentId = destinationFiscal?.Id,
                    DestinationFiscalDocumentStatus = destinationFiscal?.Status,
                    DestinationFinalCfdiUuid = destinationStamp?.Uuid,
                    DestinationFinalCfdiSeries = destinationFiscal?.Series,
                    DestinationFinalCfdiFolio = destinationFiscal?.Folio,
                    DestinationStampedAtUtc = destinationStamp?.StampedAtUtc,
                    AssignedAtUtc = assignment.AssignedAtUtc,
                    AssignedByDisplayName = assignment.AssignedByDisplayName,
                    ReleasedAtUtc = assignment.ReleasedAtUtc,
                    ReleasedByDisplayName = assignment.ReleasedByDisplayName
                };
            }).ToArray();

            var (currentTraceStatus, currentTraceMessage) = ResolveTraceStatus(entry.Removal, activeAssignment, currentDestinationFiscal, currentDestinationStamp);

            result.Add(new BillingDocumentRemovedItemTraceModel
            {
                RemovalId = entry.Removal.Id,
                BillingDocumentId = entry.Removal.BillingDocumentId,
                FiscalDocumentId = entry.Removal.FiscalDocumentId,
                SalesOrderId = entry.Removal.SalesOrderId,
                SalesOrderItemId = entry.Removal.SalesOrderItemId,
                SourceLegacyOrderId = entry.Removal.SourceLegacyOrderId,
                CustomerName = entry.CustomerName,
                SourceSalesOrderLineNumber = entry.Removal.SourceSalesOrderLineNumber,
                ProductInternalCode = entry.Removal.ProductInternalCode,
                Description = entry.Removal.Description,
                QuantityRemoved = entry.Removal.QuantityRemoved,
                RemovalReason = entry.Removal.RemovalReason.ToString(),
                Observations = entry.Removal.Observations,
                RemovalDisposition = entry.Removal.RemovalDisposition.ToString(),
                AvailableForPendingBillingReuse = entry.Removal.AvailableForPendingBillingReuse,
                RemovedAtUtc = entry.Removal.RemovedAtUtc,
                CurrentTraceStatus = currentTraceStatus,
                CurrentTraceMessage = currentTraceMessage,
                CurrentDestinationBillingDocumentId = activeAssignment?.DestinationBillingDocumentId,
                CurrentDestinationBillingDocumentStatus = currentDestinationBilling?.Status,
                CurrentDestinationFiscalDocumentId = currentDestinationFiscal?.Id,
                CurrentDestinationFiscalDocumentStatus = currentDestinationFiscal?.Status,
                FinalCfdiUuid = currentDestinationStamp?.Uuid,
                FinalCfdiSeries = currentDestinationFiscal?.Series,
                FinalCfdiFolio = currentDestinationFiscal?.Folio,
                FinalStampedAtUtc = currentDestinationStamp?.StampedAtUtc,
                AssignmentHistory = assignmentTrace
            });
        }

        return result;
    }

    private static (string Status, string Message) ResolveTraceStatus(
        Domain.Entities.BillingDocumentItemRemoval removal,
        Domain.Entities.BillingDocumentPendingItemAssignment? activeAssignment,
        DestinationFiscalDocumentProjection? currentDestinationFiscal,
        DestinationStampProjection? currentDestinationStamp)
    {
        if (removal.RemovalDisposition == Domain.Enums.BillingDocumentItemRemovalDisposition.ExcludedDefinitively)
        {
            return ("ExcludedDefinitively", "Producto excluido definitivamente del ciclo de facturación.");
        }

        if (activeAssignment is null)
        {
            return removal.AvailableForPendingBillingReuse
                ? ("PendingBillingAvailable", "Producto disponible para reasignarse manualmente a otro documento fiscal.")
                : ("PendingBillingConsumed", "Producto removido con trazabilidad histórica, pero ya no disponible como pendiente libre.");
        }

        if (currentDestinationStamp?.Uuid is string uuid && !string.IsNullOrWhiteSpace(uuid))
        {
            return ("ReassignedAndStamped", $"Producto reasignado y timbrado finalmente en el CFDI {uuid}.");
        }

        if (currentDestinationFiscal is not null)
        {
            return ("ReassignedToFiscalDocument", $"Producto reasignado al documento fiscal #{currentDestinationFiscal.Id}, todavía sin CFDI timbrado final.");
        }

        return ("ReassignedToBillingDocument", $"Producto reasignado al documento de facturación #{activeAssignment.DestinationBillingDocumentId}, pendiente de preparar CFDI.");
    }

    private sealed class DestinationFiscalDocumentProjection
    {
        public long Id { get; init; }

        public long BillingDocumentId { get; init; }

        public string Status { get; init; } = string.Empty;

        public string? Series { get; init; }

        public string? Folio { get; init; }
    }

    private sealed class DestinationBillingDocumentProjection
    {
        public long Id { get; init; }

        public string Status { get; init; } = string.Empty;
    }

    private sealed class DestinationStampProjection
    {
        public long FiscalDocumentId { get; init; }

        public string? Uuid { get; init; }

        public DateTime? StampedAtUtc { get; init; }
    }
}
