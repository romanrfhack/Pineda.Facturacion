namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class InternalRepBaseDocumentPaymentComplementReadModel
{
    public long PaymentComplementId { get; init; }

    public long AccountsReceivablePaymentId { get; init; }

    public string Status { get; init; } = string.Empty;

    public string? Uuid { get; init; }

    public DateTime PaymentDateUtc { get; init; }

    public DateTime? IssuedAtUtc { get; init; }

    public DateTime? StampedAtUtc { get; init; }

    public decimal PaidAmount { get; init; }
}
