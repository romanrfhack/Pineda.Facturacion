namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class StampExternalRepBaseDocumentPaymentComplementCommand
{
    public long ExternalRepBaseDocumentId { get; set; }

    public long? PaymentComplementDocumentId { get; set; }

    public bool RetryRejected { get; set; }
}
