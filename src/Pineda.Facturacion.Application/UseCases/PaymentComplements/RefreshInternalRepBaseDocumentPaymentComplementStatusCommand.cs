namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class RefreshInternalRepBaseDocumentPaymentComplementStatusCommand
{
    public long FiscalDocumentId { get; init; }

    public long? PaymentComplementDocumentId { get; init; }
}
