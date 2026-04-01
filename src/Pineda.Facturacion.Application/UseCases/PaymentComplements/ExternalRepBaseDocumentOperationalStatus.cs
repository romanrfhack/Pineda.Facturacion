namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public enum ExternalRepBaseDocumentOperationalStatus
{
    Imported = 1,
    ReadyForPayment = 2,
    ReadyForRepPreparation = 3,
    ReadyForRepStamping = 4,
    RepIssued = 5,
    Blocked = 6
}
