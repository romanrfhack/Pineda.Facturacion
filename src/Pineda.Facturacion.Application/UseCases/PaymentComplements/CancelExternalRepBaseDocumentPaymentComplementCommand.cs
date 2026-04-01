namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class CancelExternalRepBaseDocumentPaymentComplementCommand
{
    public long ExternalRepBaseDocumentId { get; init; }

    public long? PaymentComplementDocumentId { get; init; }

    public string CancellationReasonCode { get; init; } = "02";

    public string? ReplacementUuid { get; init; }
}
