namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public enum StampPaymentComplementOutcome
{
    Stamped = 0,
    NotFound = 1,
    Conflict = 2,
    ValidationFailed = 3,
    ProviderRejected = 4,
    ProviderUnavailable = 5
}
