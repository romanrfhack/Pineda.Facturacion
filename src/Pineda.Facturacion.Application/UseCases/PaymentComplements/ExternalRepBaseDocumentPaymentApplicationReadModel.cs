namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class ExternalRepBaseDocumentPaymentApplicationReadModel
{
    public long AccountsReceivablePaymentId { get; init; }

    public int ApplicationSequence { get; init; }

    public DateTime PaymentDateUtc { get; init; }

    public string PaymentFormSat { get; init; } = string.Empty;

    public decimal AppliedAmount { get; init; }

    public decimal PreviousBalance { get; init; }

    public decimal NewBalance { get; init; }

    public string? Reference { get; init; }

    public string? Notes { get; init; }

    public decimal PaymentAmount { get; init; }

    public decimal RemainingPaymentAmount { get; init; }

    public DateTime CreatedAtUtc { get; init; }
}
