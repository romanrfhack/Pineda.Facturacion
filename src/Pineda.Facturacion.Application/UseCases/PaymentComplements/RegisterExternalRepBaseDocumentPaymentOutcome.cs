namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public enum RegisterExternalRepBaseDocumentPaymentOutcome
{
    RegisteredAndApplied = 0,
    ValidationFailed = 1,
    NotFound = 2,
    Conflict = 3
}
