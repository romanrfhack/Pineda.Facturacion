namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public enum ExternalRepBaseDocumentOperationalStatus
{
    Imported = 1,
    ReadyForNextPhase = 2,
    Blocked = 3
}
