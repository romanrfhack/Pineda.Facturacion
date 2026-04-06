namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class InternalRepBaseDocumentPaymentComplementReadModel
{
    public long PaymentComplementId { get; init; }

    public long AccountsReceivablePaymentId { get; init; }

    public string Status { get; init; } = string.Empty;

    public string? Uuid { get; init; }

    public DateTime PaymentDateUtc { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime UpdatedAtUtc { get; init; }

    public DateTime? IssuedAtUtc { get; init; }

    public DateTime? StampCreatedAtUtc { get; init; }

    public DateTime? StampUpdatedAtUtc { get; init; }

    public DateTime? StampedAtUtc { get; init; }

    public DateTime? LastStatusCheckAtUtc { get; init; }

    public string? LastKnownExternalStatus { get; init; }

    public string? LastStatusProviderCode { get; init; }

    public string? LastStatusProviderMessage { get; init; }

    public string? CancellationStatus { get; init; }

    public DateTime? CancellationRequestedAtUtc { get; init; }

    public DateTime? CancellationCreatedAtUtc { get; init; }

    public DateTime? CancellationUpdatedAtUtc { get; init; }

    public DateTime? CancelledAtUtc { get; init; }

    public string? ProviderName { get; init; }

    public int InstallmentNumber { get; init; }

    public decimal PreviousBalance { get; init; }

    public decimal PaidAmount { get; init; }

    public decimal RemainingBalance { get; init; }
}
