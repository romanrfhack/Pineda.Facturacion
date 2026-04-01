namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class PrepareInternalRepBaseDocumentPaymentComplementCommand
{
    public long FiscalDocumentId { get; set; }

    public long? AccountsReceivablePaymentId { get; set; }
}
