namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class StampInternalRepBaseDocumentPaymentComplementCommand
{
    public long FiscalDocumentId { get; set; }

    public long? PaymentComplementDocumentId { get; set; }

    public bool RetryRejected { get; set; }
}
