using System.Globalization;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Models.Legacy;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.Orders;

public enum OrderDebtSummaryEligibilityClassification
{
    PendingBilling,
    PendingStamping,
    OpenReceivable,
    PartiallyPaidReceivable,
    PaidReceivable,
    OverpaidReceivable,
    CancelledDocument,
    StampedPue,
    ReceivableStateUnavailable,
    InconsistentReceivableState,
    UnconfirmedDocumentMembership,
    AmbiguousFiscalTrace,
    CancellationPending
}

public enum OrderDebtSummarySourceType
{
    PendingOrder,
    ReceivableInvoice
}

public sealed class OrderDebtSummaryEligibilityDecision
{
    public bool CanInclude { get; init; }

    public bool RequiresReview { get; init; }

    public OrderDebtSummaryEligibilityClassification Classification { get; init; }

    public string ReasonCode { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string DisplayStatus { get; init; } = string.Empty;

    public OrderDebtSummarySourceType SourceType { get; init; }

    public string ReportGroupKey { get; init; } = string.Empty;

    public decimal AmountDue { get; init; }

    public string CurrencyCode { get; init; } = "MXN";
}

public static class OrderDebtSummaryEligibilityPolicy
{
    private const decimal MoneyTolerance = 0.01m;

    public static OrderDebtSummaryEligibilityDecision Evaluate(
        LegacyOrderReadModel order,
        OrderDebtTraceSnapshot? trace)
    {
        ArgumentNullException.ThrowIfNull(order);

        var orderCurrency = NormalizeCurrency(order.CurrencyCode);
        var orderTotal = NormalizeMoney(order.Total);
        if (trace?.BillingDocumentId is null)
        {
            return IncludePendingOrder(
                order,
                OrderDebtSummaryEligibilityClassification.PendingBilling,
                "OrderDebtSummary.PendingBilling",
                "La orden todavía no está asociada a un documento de facturación.",
                "Pendiente de facturar",
                orderTotal,
                orderCurrency);
        }

        if (trace.ConfirmedOperationalDocumentCount > 1)
        {
            return ReviewRequired(
                order,
                trace,
                OrderDebtSummaryEligibilityClassification.AmbiguousFiscalTrace,
                "OrderDebtSummary.AmbiguousFiscalTrace",
                $"La orden está relacionada con {trace.ConfirmedOperationalDocumentCount} documentos operativos. No es seguro determinar cuál controla el adeudo.");
        }

        if (trace.HasDirectLinkMismatch)
        {
            return ReviewRequired(
                order,
                trace,
                OrderDebtSummaryEligibilityClassification.UnconfirmedDocumentMembership,
                "OrderDebtSummary.DirectLinkMismatch",
                "El enlace directo de la orden no coincide con la composición real del documento de facturación.");
        }

        if (!trace.MembershipConfirmed)
        {
            return ReviewRequired(
                order,
                trace,
                OrderDebtSummaryEligibilityClassification.UnconfirmedDocumentMembership,
                "OrderDebtSummary.UnconfirmedDocumentMembership",
                "La orden apunta a un documento de facturación, pero no se encontró evidencia en la orden principal ni en sus conceptos.");
        }

        if (trace.BillingDocumentStatus == BillingDocumentStatus.Cancelled)
        {
            return Block(
                order,
                trace,
                OrderDebtSummaryEligibilityClassification.CancelledDocument,
                "OrderDebtSummary.BillingDocumentCancelled",
                "El documento de facturación asociado está cancelado.",
                "Documento cancelado");
        }

        if (trace.FiscalDocumentId is null)
        {
            return IncludePendingOrder(
                order,
                OrderDebtSummaryEligibilityClassification.PendingStamping,
                "OrderDebtSummary.PendingStamping",
                "La orden tiene documento de facturación, pero todavía no cuenta con un CFDI operativo.",
                "Pendiente de timbrar",
                orderTotal,
                orderCurrency,
                trace);
        }

        if (trace.FiscalDocumentStatus == FiscalDocumentStatus.Cancelled)
        {
            return Block(
                order,
                trace,
                OrderDebtSummaryEligibilityClassification.CancelledDocument,
                "OrderDebtSummary.FiscalDocumentCancelled",
                $"El CFDI {BuildFiscalReference(trace)} está cancelado.",
                "CFDI cancelado");
        }

        if (trace.FiscalDocumentStatus == FiscalDocumentStatus.CancellationRequested)
        {
            return ReviewRequired(
                order,
                trace,
                OrderDebtSummaryEligibilityClassification.CancellationPending,
                "OrderDebtSummary.FiscalCancellationPending",
                $"El CFDI {BuildFiscalReference(trace)} tiene una cancelación en proceso.");
        }

        var isStampedOperational = trace.FiscalDocumentStatus is FiscalDocumentStatus.Stamped
            or FiscalDocumentStatus.CancellationRejected;
        if (!isStampedOperational)
        {
            return IncludePendingOrder(
                order,
                OrderDebtSummaryEligibilityClassification.PendingStamping,
                "OrderDebtSummary.PendingStamping",
                $"El documento fiscal está en estado '{trace.FiscalDocumentStatus}' y aún no representa una factura cobrada.",
                $"Pendiente fiscal: {trace.FiscalDocumentStatus}",
                orderTotal,
                orderCurrency,
                trace);
        }

        if (string.IsNullOrWhiteSpace(trace.FiscalUuid))
        {
            return ReviewRequired(
                order,
                trace,
                OrderDebtSummaryEligibilityClassification.ReceivableStateUnavailable,
                "OrderDebtSummary.StampedEvidenceUnavailable",
                "El documento aparece timbrado, pero no existe evidencia persistida de UUID exitoso.");
        }

        var paymentMethod = NormalizeCode(trace.PaymentMethodSat);
        if (paymentMethod == "PUE")
        {
            return Block(
                order,
                trace,
                OrderDebtSummaryEligibilityClassification.StampedPue,
                "OrderDebtSummary.StampedPue",
                $"El CFDI {BuildFiscalReference(trace)} fue timbrado como PUE y no debe enviarse como adeudo pendiente.",
                "CFDI PUE · No corresponde a adeudo");
        }

        if (paymentMethod != "PPD")
        {
            return ReviewRequired(
                order,
                trace,
                OrderDebtSummaryEligibilityClassification.ReceivableStateUnavailable,
                "OrderDebtSummary.UnsupportedPaymentMethod",
                $"El CFDI {BuildFiscalReference(trace)} tiene método de pago '{paymentMethod}' y no puede clasificarse automáticamente como PPD o PUE.");
        }

        if (!trace.AccountsReceivableInvoiceId.HasValue
            || !trace.AccountsReceivableStatus.HasValue
            || !trace.InvoiceTotal.HasValue
            || !trace.PaidTotal.HasValue
            || !trace.OutstandingBalance.HasValue)
        {
            return ReviewRequired(
                order,
                trace,
                OrderDebtSummaryEligibilityClassification.ReceivableStateUnavailable,
                "OrderDebtSummary.PpdReceivableMissing",
                $"El CFDI PPD {BuildFiscalReference(trace)} no tiene un registro completo y verificable en Cuentas por Cobrar.");
        }

        var currency = NormalizeCurrency(trace.ReceivableCurrencyCode ?? trace.FiscalCurrencyCode ?? order.CurrencyCode);
        var invoiceTotal = NormalizeMoney(trace.InvoiceTotal.Value);
        var paidTotal = NormalizeMoney(trace.PaidTotal.Value);
        var outstandingBalance = NormalizeMoney(trace.OutstandingBalance.Value);
        var fiscalTotal = trace.FiscalTotal.HasValue ? NormalizeMoney(trace.FiscalTotal.Value) : (decimal?)null;

        if (invoiceTotal < 0m
            || paidTotal < 0m
            || outstandingBalance < 0m
            || !ApproximatelyEqual(invoiceTotal, paidTotal + outstandingBalance)
            || (fiscalTotal.HasValue && !ApproximatelyEqual(invoiceTotal, fiscalTotal.Value))
            || !CurrenciesMatch(trace.FiscalCurrencyCode, trace.ReceivableCurrencyCode))
        {
            return Inconsistent(
                order,
                trace,
                invoiceTotal,
                paidTotal,
                outstandingBalance,
                currency,
                "Los totales, el saldo, la moneda o el importe fiscal no son consistentes entre el CFDI y Cuentas por Cobrar.");
        }

        return trace.AccountsReceivableStatus.Value switch
        {
            AccountsReceivableInvoiceStatus.Open when
                outstandingBalance > 0m
                && ApproximatelyZero(paidTotal)
                && ApproximatelyEqual(outstandingBalance, invoiceTotal)
                => IncludeReceivable(
                    order,
                    trace,
                    OrderDebtSummaryEligibilityClassification.OpenReceivable,
                    "OrderDebtSummary.OpenReceivable",
                    $"La factura está pendiente. Se incluirá únicamente su saldo actual de {FormatMoney(outstandingBalance, currency)}.",
                    $"CxC pendiente · Saldo {FormatMoney(outstandingBalance, currency)}",
                    outstandingBalance,
                    currency),

            AccountsReceivableInvoiceStatus.PartiallyPaid when
                paidTotal > 0m
                && outstandingBalance > 0m
                && outstandingBalance < invoiceTotal
                => IncludeReceivable(
                    order,
                    trace,
                    OrderDebtSummaryEligibilityClassification.PartiallyPaidReceivable,
                    "OrderDebtSummary.PartiallyPaidReceivable",
                    $"La factura tiene pagos aplicados. Se incluirá únicamente el saldo real de {FormatMoney(outstandingBalance, currency)}.",
                    $"Pago parcial · Factura {FormatMoney(invoiceTotal, currency)} · Pagado {FormatMoney(paidTotal, currency)} · Saldo {FormatMoney(outstandingBalance, currency)}",
                    outstandingBalance,
                    currency),

            AccountsReceivableInvoiceStatus.Paid when
                ApproximatelyZero(outstandingBalance)
                && ApproximatelyEqual(paidTotal, invoiceTotal)
                => Block(
                    order,
                    trace,
                    OrderDebtSummaryEligibilityClassification.PaidReceivable,
                    "OrderDebtSummary.InvoicePaid",
                    $"La factura CxC #{trace.AccountsReceivableInvoiceId} correspondiente al CFDI {BuildFiscalReference(trace)} ya está pagada.",
                    "Pagada · Saldo 0.00"),

            AccountsReceivableInvoiceStatus.Overpaid
                => Block(
                    order,
                    trace,
                    OrderDebtSummaryEligibilityClassification.OverpaidReceivable,
                    "OrderDebtSummary.InvoiceOverpaid",
                    $"La factura CxC #{trace.AccountsReceivableInvoiceId} está marcada como sobrepagada y no tiene un adeudo exigible.",
                    "Sobrepagada · Sin adeudo"),

            AccountsReceivableInvoiceStatus.Cancelled
                => Block(
                    order,
                    trace,
                    OrderDebtSummaryEligibilityClassification.CancelledDocument,
                    "OrderDebtSummary.ReceivableCancelled",
                    $"La factura CxC #{trace.AccountsReceivableInvoiceId} está cancelada.",
                    "CxC cancelada"),

            _ => Inconsistent(
                order,
                trace,
                invoiceTotal,
                paidTotal,
                outstandingBalance,
                currency,
                $"El estado '{trace.AccountsReceivableStatus}' no coincide con los importes registrados.")
        };
    }

    public static string BuildFiscalReference(OrderDebtTraceSnapshot trace)
    {
        var series = trace.FiscalSeries?.Trim();
        var folio = trace.FiscalFolio?.Trim();
        if (!string.IsNullOrWhiteSpace(series) || !string.IsNullOrWhiteSpace(folio))
        {
            return string.Join("-", new[] { series, folio }.Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        if (!string.IsNullOrWhiteSpace(trace.FiscalUuid))
        {
            return trace.FiscalUuid.Trim();
        }

        return trace.FiscalDocumentId.HasValue
            ? $"#{trace.FiscalDocumentId.Value}"
            : "sin referencia";
    }

    private static OrderDebtSummaryEligibilityDecision IncludePendingOrder(
        LegacyOrderReadModel order,
        OrderDebtSummaryEligibilityClassification classification,
        string reasonCode,
        string message,
        string displayStatus,
        decimal amountDue,
        string currencyCode,
        OrderDebtTraceSnapshot? trace = null)
    {
        return new OrderDebtSummaryEligibilityDecision
        {
            CanInclude = true,
            Classification = classification,
            ReasonCode = reasonCode,
            Message = message,
            DisplayStatus = $"{displayStatus} · Importe {FormatMoney(amountDue, currencyCode)}",
            SourceType = OrderDebtSummarySourceType.PendingOrder,
            ReportGroupKey = $"ORDER:{order.LegacyOrderId}",
            AmountDue = amountDue,
            CurrencyCode = currencyCode
        };
    }

    private static OrderDebtSummaryEligibilityDecision IncludeReceivable(
        LegacyOrderReadModel order,
        OrderDebtTraceSnapshot trace,
        OrderDebtSummaryEligibilityClassification classification,
        string reasonCode,
        string message,
        string displayStatus,
        decimal amountDue,
        string currencyCode)
    {
        return new OrderDebtSummaryEligibilityDecision
        {
            CanInclude = true,
            Classification = classification,
            ReasonCode = reasonCode,
            Message = message,
            DisplayStatus = displayStatus,
            SourceType = OrderDebtSummarySourceType.ReceivableInvoice,
            ReportGroupKey = $"AR:{trace.AccountsReceivableInvoiceId}",
            AmountDue = amountDue,
            CurrencyCode = currencyCode
        };
    }

    private static OrderDebtSummaryEligibilityDecision Block(
        LegacyOrderReadModel order,
        OrderDebtTraceSnapshot trace,
        OrderDebtSummaryEligibilityClassification classification,
        string reasonCode,
        string message,
        string displayStatus)
    {
        return new OrderDebtSummaryEligibilityDecision
        {
            CanInclude = false,
            Classification = classification,
            ReasonCode = reasonCode,
            Message = message,
            DisplayStatus = displayStatus,
            SourceType = trace.AccountsReceivableInvoiceId.HasValue
                ? OrderDebtSummarySourceType.ReceivableInvoice
                : OrderDebtSummarySourceType.PendingOrder,
            ReportGroupKey = trace.AccountsReceivableInvoiceId.HasValue
                ? $"AR:{trace.AccountsReceivableInvoiceId.Value}"
                : $"ORDER:{order.LegacyOrderId}",
            CurrencyCode = NormalizeCurrency(trace.ReceivableCurrencyCode ?? trace.FiscalCurrencyCode ?? order.CurrencyCode)
        };
    }

    private static OrderDebtSummaryEligibilityDecision ReviewRequired(
        LegacyOrderReadModel order,
        OrderDebtTraceSnapshot trace,
        OrderDebtSummaryEligibilityClassification classification,
        string reasonCode,
        string message)
    {
        return new OrderDebtSummaryEligibilityDecision
        {
            CanInclude = false,
            RequiresReview = true,
            Classification = classification,
            ReasonCode = reasonCode,
            Message = message,
            DisplayStatus = "Revisión requerida",
            SourceType = trace.AccountsReceivableInvoiceId.HasValue
                ? OrderDebtSummarySourceType.ReceivableInvoice
                : OrderDebtSummarySourceType.PendingOrder,
            ReportGroupKey = trace.AccountsReceivableInvoiceId.HasValue
                ? $"AR:{trace.AccountsReceivableInvoiceId.Value}"
                : $"ORDER:{order.LegacyOrderId}",
            CurrencyCode = NormalizeCurrency(trace.ReceivableCurrencyCode ?? trace.FiscalCurrencyCode ?? order.CurrencyCode)
        };
    }

    private static OrderDebtSummaryEligibilityDecision Inconsistent(
        LegacyOrderReadModel order,
        OrderDebtTraceSnapshot trace,
        decimal invoiceTotal,
        decimal paidTotal,
        decimal outstandingBalance,
        string currencyCode,
        string detail)
    {
        return ReviewRequired(
            order,
            trace,
            OrderDebtSummaryEligibilityClassification.InconsistentReceivableState,
            "OrderDebtSummary.InconsistentReceivableState",
            $"{detail} CxC #{trace.AccountsReceivableInvoiceId}: total {FormatMoney(invoiceTotal, currencyCode)}, pagado {FormatMoney(paidTotal, currencyCode)}, saldo {FormatMoney(outstandingBalance, currencyCode)}.");
    }

    private static bool CurrenciesMatch(string? fiscalCurrency, string? receivableCurrency)
    {
        if (string.IsNullOrWhiteSpace(fiscalCurrency) || string.IsNullOrWhiteSpace(receivableCurrency))
        {
            return true;
        }

        return string.Equals(
            NormalizeCurrency(fiscalCurrency),
            NormalizeCurrency(receivableCurrency),
            StringComparison.Ordinal);
    }

    private static string NormalizeCode(string? value)
        => (value ?? string.Empty).Trim().ToUpperInvariant();

    private static string NormalizeCurrency(string? value)
        => string.IsNullOrWhiteSpace(value) ? "MXN" : value.Trim().ToUpperInvariant();

    private static decimal NormalizeMoney(decimal value)
        => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static bool ApproximatelyEqual(decimal left, decimal right)
        => Math.Abs(NormalizeMoney(left) - NormalizeMoney(right)) <= MoneyTolerance;

    private static bool ApproximatelyZero(decimal value)
        => Math.Abs(NormalizeMoney(value)) <= MoneyTolerance;

    private static string FormatMoney(decimal value, string currencyCode)
        => $"{NormalizeMoney(value).ToString("N2", CultureInfo.GetCultureInfo("es-MX"))} {NormalizeCurrency(currencyCode)}";
}
