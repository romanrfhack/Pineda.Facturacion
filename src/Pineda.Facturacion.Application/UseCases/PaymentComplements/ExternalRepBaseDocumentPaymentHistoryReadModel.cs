namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class ExternalRepBaseDocumentPaymentHistoryReadModel
{
    public long AccountsReceivablePaymentId { get; init; }

    public DateTime PaymentDateUtc { get; init; }

    public string PaymentFormSat { get; init; } = string.Empty;

    public decimal PaymentAmount { get; init; }

    public decimal AmountAppliedToDocument { get; init; }

    public decimal RemainingPaymentAmount { get; init; }

    public string? Reference { get; init; }

    public string? Notes { get; init; }

    public long? PaymentComplementId { get; init; }

    public string? PaymentComplementStatus { get; init; }

    public string? PaymentComplementUuid { get; init; }

    public DateTime CreatedAtUtc { get; init; }
}
