namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public enum PrepareInternalRepBaseDocumentPaymentComplementOutcome
{
    Prepared = 0,
    AlreadyPrepared = 1,
    NotFound = 2,
    ValidationFailed = 3,
    Conflict = 4
}
