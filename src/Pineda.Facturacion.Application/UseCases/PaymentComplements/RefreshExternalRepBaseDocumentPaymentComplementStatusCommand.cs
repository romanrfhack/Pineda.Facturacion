namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class RefreshExternalRepBaseDocumentPaymentComplementStatusCommand
{
    public long ExternalRepBaseDocumentId { get; init; }

    public long? PaymentComplementDocumentId { get; init; }
}
