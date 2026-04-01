namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public enum PrepareExternalRepBaseDocumentPaymentComplementOutcome
{
    Prepared = 0,
    AlreadyPrepared = 1,
    ValidationFailed = 2,
    NotFound = 3,
    Conflict = 4
}
