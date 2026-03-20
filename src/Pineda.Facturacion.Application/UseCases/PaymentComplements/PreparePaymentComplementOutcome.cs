namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public enum PreparePaymentComplementOutcome
{
    Created = 0,
    NotFound = 1,
    Conflict = 2,
    ValidationFailed = 3
}
