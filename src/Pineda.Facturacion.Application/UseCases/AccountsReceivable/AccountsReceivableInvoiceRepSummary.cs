namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public sealed class AccountsReceivableInvoiceRepSummary
{
    public long PaymentComplementId { get; init; }

    public long AccountsReceivablePaymentId { get; init; }

    public string Status { get; init; } = string.Empty;

    public decimal TotalPaymentsAmount { get; init; }

    public DateTime IssuedAtUtc { get; init; }

    public DateTime PaymentDateUtc { get; init; }

    public string? Uuid { get; init; }

    public DateTime? StampedAtUtc { get; init; }

    public DateTime? CancelledAtUtc { get; init; }
}
