namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public enum RefreshPaymentComplementStatusOutcome
{
    Refreshed = 0,
    NotFound = 1,
    ValidationFailed = 2,
    ProviderUnavailable = 3
}
