namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class ExternalRepBaseDocumentPaymentComplementReadModel
{
    public long PaymentComplementId { get; init; }

    public long AccountsReceivablePaymentId { get; init; }

    public string Status { get; init; } = string.Empty;

    public string? Uuid { get; init; }

    public DateTime PaymentDateUtc { get; init; }

    public DateTime? IssuedAtUtc { get; init; }

    public DateTime? StampedAtUtc { get; init; }

    public DateTime? CancelledAtUtc { get; init; }

    public string? ProviderName { get; init; }

    public int InstallmentNumber { get; init; }

    public decimal PreviousBalance { get; init; }

    public decimal PaidAmount { get; init; }

    public decimal RemainingBalance { get; init; }
}
