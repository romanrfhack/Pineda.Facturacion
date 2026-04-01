namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class PrepareExternalRepBaseDocumentPaymentComplementCommand
{
    public long ExternalRepBaseDocumentId { get; set; }

    public long? AccountsReceivablePaymentId { get; set; }
}
