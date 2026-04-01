namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public enum RegisterInternalRepBaseDocumentPaymentOutcome
{
    RegisteredAndApplied = 0,
    NotFound = 1,
    ValidationFailed = 2,
    Conflict = 3
}
