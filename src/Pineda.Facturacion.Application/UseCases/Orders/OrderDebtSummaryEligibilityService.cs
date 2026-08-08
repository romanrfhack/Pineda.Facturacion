using Pineda.Facturacion.Application.Abstractions.Legacy;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Models.Legacy;

namespace Pineda.Facturacion.Application.UseCases.Orders;

public sealed class OrderDebtSummaryEligibilityService
{
    public const int MaxSelectableOrders = 50;

    private readonly ILegacyOrderReader _legacyOrderReader;
    private readonly IOrderDebtTraceReader _orderDebtTraceReader;

    public OrderDebtSummaryEligibilityService(
        ILegacyOrderReader legacyOrderReader,
        IOrderDebtTraceReader orderDebtTraceReader)
    {
        _legacyOrderReader = legacyOrderReader;
        _orderDebtTraceReader = orderDebtTraceReader;
    }

    public async Task<OrderDebtSummaryEligibilityResolution> EvaluateAsync(
        IReadOnlyCollection<string> legacyOrderIds,
        CancellationToken cancellationToken = default)
    {
        var requestedOrderIds = NormalizeLegacyOrderIds(legacyOrderIds);
        if (requestedOrderIds.Count == 0)
        {
            return Failure(requestedOrderIds, "Selecciona al menos una orden para continuar.");
        }

        if (requestedOrderIds.Count > MaxSelectableOrders)
        {
            return Failure(
                requestedOrderIds,
                $"Puedes validar hasta {MaxSelectableOrders} órdenes a la vez.");
        }

        var legacyOrders = new List<LegacyOrderReadModel>(requestedOrderIds.Count);
        var missingOrderIds = new List<string>();
        foreach (var legacyOrderId in requestedOrderIds)
        {
            var order = await _legacyOrderReader.GetByIdAsync(legacyOrderId, cancellationToken);
            if (order is null)
            {
                missingOrderIds.Add(legacyOrderId);
                continue;
            }

            legacyOrders.Add(order);
        }

        if (missingOrderIds.Count > 0)
        {
            return new OrderDebtSummaryEligibilityResolution
            {
                IsSuccess = false,
                ErrorMessage = $"Algunas órdenes seleccionadas ya no están disponibles: {string.Join(", ", missingOrderIds)}.",
                RequestedOrderIds = requestedOrderIds,
                LegacyOrders = legacyOrders,
                MissingOrderIds = missingOrderIds
            };
        }

        var traceLookup = await _orderDebtTraceReader.GetByLegacyOrderIdsAsync(requestedOrderIds, cancellationToken);
        var rawItems = legacyOrders
            .OrderBy(order => requestedOrderIds.IndexOf(order.LegacyOrderId))
            .Select(order =>
            {
                traceLookup.TryGetValue(order.LegacyOrderId, out var trace);
                return new OrderDebtSummaryEligibilityItem
                {
                    LegacyOrder = order,
                    Trace = trace,
                    Decision = OrderDebtSummaryEligibilityPolicy.Evaluate(order, trace)
                };
            })
            .ToArray();

        var items = ApplyAmountContributions(rawItems);
        return new OrderDebtSummaryEligibilityResolution
        {
            IsSuccess = true,
            RequestedOrderIds = requestedOrderIds,
            LegacyOrders = legacyOrders,
            Items = items,
            ReportOrders = BuildReportOrders(items)
        };
    }

    private static IReadOnlyList<OrderDebtSummaryEligibilityItem> ApplyAmountContributions(
        IReadOnlyList<OrderDebtSummaryEligibilityItem> items)
    {
        var result = new List<OrderDebtSummaryEligibilityItem>(items.Count);
        var accountedGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            var contribution = 0m;
            if (item.Decision.CanInclude && accountedGroups.Add(item.Decision.ReportGroupKey))
            {
                contribution = item.Decision.AmountDue;
            }

            result.Add(new OrderDebtSummaryEligibilityItem
            {
                LegacyOrder = item.LegacyOrder,
                Trace = item.Trace,
                Decision = item.Decision,
                AmountDueContribution = contribution
            });
        }

        return result;
    }

    private static IReadOnlyList<OrderDebtSummaryOrder> BuildReportOrders(
        IReadOnlyList<OrderDebtSummaryEligibilityItem> items)
    {
        var eligibleItems = items.Where(item => item.Decision.CanInclude).ToArray();
        var result = new List<OrderDebtSummaryOrder>();
        var processedGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in eligibleItems)
        {
            if (!processedGroups.Add(item.Decision.ReportGroupKey))
            {
                continue;
            }

            if (item.Decision.SourceType == OrderDebtSummarySourceType.ReceivableInvoice)
            {
                var group = eligibleItems
                    .Where(candidate => string.Equals(
                        candidate.Decision.ReportGroupKey,
                        item.Decision.ReportGroupKey,
                        StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                result.Add(BuildReceivableReportOrder(group));
                continue;
            }

            result.Add(BuildPendingOrderReportOrder(item));
        }

        return result;
    }

    private static OrderDebtSummaryOrder BuildPendingOrderReportOrder(
        OrderDebtSummaryEligibilityItem item)
    {
        var order = item.LegacyOrder;
        var trace = item.Trace;
        return new OrderDebtSummaryOrder
        {
            LegacyOrderId = order.LegacyOrderId,
            OrderDateUtc = order.OrderDateUtc,
            LegacyOrderNumber = string.IsNullOrWhiteSpace(order.LegacyOrderNumber)
                ? order.LegacyOrderId
                : order.LegacyOrderNumber.Trim(),
            LegacyOrderType = order.LegacyOrderType?.Trim(),
            CustomerName = order.CustomerName.Trim(),
            CustomerRfc = order.CustomerRfc?.Trim(),
            CurrencyCode = item.Decision.CurrencyCode,
            Total = item.Decision.AmountDue,
            IsImported = trace?.SalesOrderId.HasValue == true,
            SalesOrderId = trace?.SalesOrderId,
            BillingDocumentId = trace?.BillingDocumentId,
            BillingDocumentStatus = trace?.BillingDocumentStatus?.ToString(),
            FiscalDocumentId = trace?.FiscalDocumentId,
            FiscalDocumentStatus = trace?.FiscalDocumentStatus?.ToString(),
            FiscalUuid = trace?.FiscalUuid,
            ImportStatus = trace?.LegacyImportRecordId.HasValue == true ? "Imported" : null,
            BillingStatusLabel = item.Decision.DisplayStatus
        };
    }

    private static OrderDebtSummaryOrder BuildReceivableReportOrder(
        IReadOnlyList<OrderDebtSummaryEligibilityItem> group)
    {
        var representative = group[0];
        var order = representative.LegacyOrder;
        var trace = representative.Trace
            ?? throw new InvalidOperationException("A receivable report row requires a financial trace.");
        var relatedOrderIds = trace.RelatedLegacyOrderIds.Count > 0
            ? trace.RelatedLegacyOrderIds
            : group.Select(item => item.LegacyOrder.LegacyOrderId).ToArray();
        var reference = OrderDebtSummaryEligibilityPolicy.BuildFiscalReference(trace);

        return new OrderDebtSummaryOrder
        {
            LegacyOrderId = order.LegacyOrderId,
            OrderDateUtc = trace.FiscalIssuedAtUtc ?? order.OrderDateUtc,
            LegacyOrderNumber = $"CFDI {reference}",
            LegacyOrderType = $"Órdenes {FormatOrderIds(relatedOrderIds)}",
            CustomerName = order.CustomerName.Trim(),
            CustomerRfc = order.CustomerRfc?.Trim(),
            CurrencyCode = representative.Decision.CurrencyCode,
            Total = representative.Decision.AmountDue,
            IsImported = true,
            SalesOrderId = trace.SalesOrderId,
            BillingDocumentId = trace.BillingDocumentId,
            BillingDocumentStatus = trace.BillingDocumentStatus?.ToString(),
            FiscalDocumentId = trace.FiscalDocumentId,
            FiscalDocumentStatus = trace.FiscalDocumentStatus?.ToString(),
            FiscalUuid = trace.FiscalUuid,
            ImportStatus = "Imported",
            BillingStatusLabel = representative.Decision.DisplayStatus
        };
    }

    private static string FormatOrderIds(IReadOnlyCollection<string> legacyOrderIds)
    {
        var ids = legacyOrderIds
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (ids.Length <= 6)
        {
            return string.Join(", ", ids);
        }

        return $"{string.Join(", ", ids.Take(6))} y {ids.Length - 6} más";
    }

    private static IReadOnlyList<string> NormalizeLegacyOrderIds(
        IReadOnlyCollection<string> legacyOrderIds)
    {
        var result = new List<string>(legacyOrderIds.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in legacyOrderIds)
        {
            var normalized = value?.Trim();
            if (string.IsNullOrWhiteSpace(normalized) || !seen.Add(normalized))
            {
                continue;
            }

            result.Add(normalized);
        }

        return result;
    }

    private static OrderDebtSummaryEligibilityResolution Failure(
        IReadOnlyList<string> requestedOrderIds,
        string errorMessage)
    {
        return new OrderDebtSummaryEligibilityResolution
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            RequestedOrderIds = requestedOrderIds
        };
    }
}

public sealed class OrderDebtSummaryEligibilityResolution
{
    public bool IsSuccess { get; init; }

    public string? ErrorMessage { get; init; }

    public IReadOnlyList<string> RequestedOrderIds { get; init; } = [];

    public IReadOnlyList<string> MissingOrderIds { get; init; } = [];

    public IReadOnlyList<LegacyOrderReadModel> LegacyOrders { get; init; } = [];

    public IReadOnlyList<OrderDebtSummaryEligibilityItem> Items { get; init; } = [];

    public IReadOnlyList<OrderDebtSummaryOrder> ReportOrders { get; init; } = [];

    public IReadOnlyList<OrderDebtSummaryEligibilityItem> BlockingItems
        => Items.Where(item => !item.Decision.CanInclude).ToArray();
}

public sealed class OrderDebtSummaryEligibilityItem
{
    public LegacyOrderReadModel LegacyOrder { get; init; } = new();

    public OrderDebtTraceSnapshot? Trace { get; init; }

    public OrderDebtSummaryEligibilityDecision Decision { get; init; } = new();

    public decimal AmountDueContribution { get; init; }
}
