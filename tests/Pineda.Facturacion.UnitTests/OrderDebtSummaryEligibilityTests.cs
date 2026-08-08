using Pineda.Facturacion.Application.Abstractions.Legacy;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Models.Legacy;
using Pineda.Facturacion.Application.UseCases.Orders;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.UnitTests;

public class OrderDebtSummaryEligibilityTests
{
    [Fact]
    public void StampedPue_IsBlockedFromDebtSummary()
    {
        var order = CreateOrder("LEG-1001", 116m);
        var trace = CreateStampedTrace(
            "LEG-1001",
            paymentMethodSat: "PUE",
            accountsReceivableStatus: null);

        var decision = OrderDebtSummaryEligibilityPolicy.Evaluate(order, trace);

        Assert.False(decision.CanInclude);
        Assert.False(decision.RequiresReview);
        Assert.Equal(OrderDebtSummaryEligibilityClassification.StampedPue, decision.Classification);
        Assert.Equal("OrderDebtSummary.StampedPue", decision.ReasonCode);
    }

    [Fact]
    public void PartiallyPaidPpd_UsesOnlyOutstandingBalance()
    {
        var order = CreateOrder("LEG-1001", 1000m);
        var trace = CreateStampedTrace(
            "LEG-1001",
            paymentMethodSat: "PPD",
            accountsReceivableStatus: AccountsReceivableInvoiceStatus.PartiallyPaid,
            invoiceTotal: 1000m,
            paidTotal: 700m,
            outstandingBalance: 300m);

        var decision = OrderDebtSummaryEligibilityPolicy.Evaluate(order, trace);

        Assert.True(decision.CanInclude);
        Assert.False(decision.RequiresReview);
        Assert.Equal(OrderDebtSummaryEligibilityClassification.PartiallyPaidReceivable, decision.Classification);
        Assert.Equal(300m, decision.AmountDue);
        Assert.Equal("AR:900", decision.ReportGroupKey);
        Assert.Contains("Saldo 300.00 MXN", decision.DisplayStatus, StringComparison.Ordinal);
    }

    [Fact]
    public void PaidPpd_IsBlockedFromDebtSummary()
    {
        var order = CreateOrder("LEG-1001", 1000m);
        var trace = CreateStampedTrace(
            "LEG-1001",
            paymentMethodSat: "PPD",
            accountsReceivableStatus: AccountsReceivableInvoiceStatus.Paid,
            invoiceTotal: 1000m,
            paidTotal: 1000m,
            outstandingBalance: 0m);

        var decision = OrderDebtSummaryEligibilityPolicy.Evaluate(order, trace);

        Assert.False(decision.CanInclude);
        Assert.Equal(OrderDebtSummaryEligibilityClassification.PaidReceivable, decision.Classification);
        Assert.Equal("OrderDebtSummary.InvoicePaid", decision.ReasonCode);
    }

    [Fact]
    public void PpdWithoutReceivable_RequiresReview()
    {
        var order = CreateOrder("LEG-1001", 1000m);
        var trace = CreateStampedTrace(
            "LEG-1001",
            paymentMethodSat: "PPD",
            accountsReceivableStatus: null);

        var decision = OrderDebtSummaryEligibilityPolicy.Evaluate(order, trace);

        Assert.False(decision.CanInclude);
        Assert.True(decision.RequiresReview);
        Assert.Equal(OrderDebtSummaryEligibilityClassification.ReceivableStateUnavailable, decision.Classification);
        Assert.Equal("OrderDebtSummary.PpdReceivableMissing", decision.ReasonCode);
    }

    [Fact]
    public void InconsistentPaidState_RequiresReviewInsteadOfGuessing()
    {
        var order = CreateOrder("LEG-1001", 1000m);
        var trace = CreateStampedTrace(
            "LEG-1001",
            paymentMethodSat: "PPD",
            accountsReceivableStatus: AccountsReceivableInvoiceStatus.Paid,
            invoiceTotal: 1000m,
            paidTotal: 700m,
            outstandingBalance: 300m);

        var decision = OrderDebtSummaryEligibilityPolicy.Evaluate(order, trace);

        Assert.False(decision.CanInclude);
        Assert.True(decision.RequiresReview);
        Assert.Equal(OrderDebtSummaryEligibilityClassification.InconsistentReceivableState, decision.Classification);
    }

    [Fact]
    public async Task MultipleOrdersFromSameInvoice_ContributeOutstandingBalanceOnce()
    {
        var firstOrder = CreateOrder("LEG-1001", 600m);
        var secondOrder = CreateOrder("LEG-1002", 400m);
        var traceReader = new FakeOrderDebtTraceReader();
        traceReader.Results["LEG-1001"] = CreateStampedTrace(
            "LEG-1001",
            paymentMethodSat: "PPD",
            accountsReceivableStatus: AccountsReceivableInvoiceStatus.PartiallyPaid,
            invoiceTotal: 1000m,
            paidTotal: 700m,
            outstandingBalance: 300m,
            relatedLegacyOrderIds: ["LEG-1001", "LEG-1002"]);
        traceReader.Results["LEG-1002"] = CreateStampedTrace(
            "LEG-1002",
            paymentMethodSat: "PPD",
            accountsReceivableStatus: AccountsReceivableInvoiceStatus.PartiallyPaid,
            invoiceTotal: 1000m,
            paidTotal: 700m,
            outstandingBalance: 300m,
            relatedLegacyOrderIds: ["LEG-1001", "LEG-1002"]);
        var service = new OrderDebtSummaryEligibilityService(
            new FakeLegacyOrderReader(firstOrder, secondOrder),
            traceReader);

        var resolution = await service.EvaluateAsync(["LEG-1001", "LEG-1002"]);

        Assert.True(resolution.IsSuccess);
        Assert.Empty(resolution.BlockingItems);
        Assert.Collection(
            resolution.Items,
            item => Assert.Equal(300m, item.AmountDueContribution),
            item => Assert.Equal(0m, item.AmountDueContribution));
        var reportOrder = Assert.Single(resolution.ReportOrders);
        Assert.Equal(300m, reportOrder.Total);
        Assert.Contains("LEG-1001", reportOrder.LegacyOrderType, StringComparison.Ordinal);
        Assert.Contains("LEG-1002", reportOrder.LegacyOrderType, StringComparison.Ordinal);
    }

    private static LegacyOrderReadModel CreateOrder(string legacyOrderId, decimal total)
    {
        return new LegacyOrderReadModel
        {
            LegacyOrderId = legacyOrderId,
            LegacyOrderNumber = legacyOrderId,
            LegacyOrderType = "Nota",
            OrderDateUtc = new DateTime(2026, 8, 7, 12, 0, 0, DateTimeKind.Utc),
            CustomerLegacyId = "C-1",
            CustomerName = "Cliente Uno",
            CustomerRfc = "AAA010101AAA",
            PaymentCondition = "Crédito",
            CurrencyCode = "MXN",
            Total = total
        };
    }

    private static OrderDebtTraceSnapshot CreateStampedTrace(
        string legacyOrderId,
        string paymentMethodSat,
        AccountsReceivableInvoiceStatus? accountsReceivableStatus,
        decimal invoiceTotal = 1000m,
        decimal paidTotal = 0m,
        decimal outstandingBalance = 1000m,
        IReadOnlyList<string>? relatedLegacyOrderIds = null)
    {
        var hasReceivable = accountsReceivableStatus.HasValue;
        return new OrderDebtTraceSnapshot
        {
            LegacyOrderId = legacyOrderId,
            LegacyImportRecordId = 1,
            SalesOrderId = legacyOrderId.EndsWith('1') ? 101 : 102,
            BillingDocumentId = 500,
            BillingDocumentStatus = BillingDocumentStatus.Stamped,
            MembershipConfirmed = true,
            MembershipEvidence = "BillingDocumentItems",
            ConfirmedOperationalDocumentCount = 1,
            RelatedLegacyOrderIds = relatedLegacyOrderIds ?? [legacyOrderId],
            FiscalDocumentId = 700,
            FiscalDocumentStatus = FiscalDocumentStatus.Stamped,
            FiscalSeries = "F",
            FiscalFolio = "123",
            FiscalUuid = "11111111-2222-3333-4444-555555555555",
            FiscalIssuedAtUtc = new DateTime(2026, 8, 7, 12, 0, 0, DateTimeKind.Utc),
            FiscalCurrencyCode = "MXN",
            FiscalTotal = invoiceTotal,
            PaymentMethodSat = paymentMethodSat,
            PaymentFormSat = paymentMethodSat == "PPD" ? "99" : "03",
            IsCreditSale = paymentMethodSat == "PPD",
            AccountsReceivableInvoiceId = hasReceivable ? 900 : null,
            AccountsReceivableStatus = accountsReceivableStatus,
            ReceivableCurrencyCode = hasReceivable ? "MXN" : null,
            InvoiceTotal = hasReceivable ? invoiceTotal : null,
            PaidTotal = hasReceivable ? paidTotal : null,
            OutstandingBalance = hasReceivable ? outstandingBalance : null
        };
    }

    private sealed class FakeLegacyOrderReader : ILegacyOrderReader
    {
        private readonly IReadOnlyDictionary<string, LegacyOrderReadModel> _orders;

        public FakeLegacyOrderReader(params LegacyOrderReadModel[] orders)
        {
            _orders = orders.ToDictionary(order => order.LegacyOrderId, StringComparer.OrdinalIgnoreCase);
        }

        public Task<LegacyOrderReadModel?> GetByIdAsync(
            string legacyOrderId,
            CancellationToken cancellationToken = default)
        {
            _orders.TryGetValue(legacyOrderId, out var order);
            return Task.FromResult(order);
        }

        public Task<LegacyOrderPageReadModel> SearchAsync(
            LegacyOrderSearchReadModel search,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new LegacyOrderPageReadModel());
    }

    private sealed class FakeOrderDebtTraceReader : IOrderDebtTraceReader
    {
        public Dictionary<string, OrderDebtTraceSnapshot> Results { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlyDictionary<string, OrderDebtTraceSnapshot>> GetByLegacyOrderIdsAsync(
            IReadOnlyCollection<string> legacyOrderIds,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyDictionary<string, OrderDebtTraceSnapshot> result = Results
                .Where(pair => legacyOrderIds.Contains(pair.Key, StringComparer.OrdinalIgnoreCase))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
            return Task.FromResult(result);
        }
    }
}
