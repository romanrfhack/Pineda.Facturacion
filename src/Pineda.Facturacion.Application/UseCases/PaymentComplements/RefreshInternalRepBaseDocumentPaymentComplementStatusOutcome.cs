namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public enum RefreshInternalRepBaseDocumentPaymentComplementStatusOutcome
{
    Refreshed = 1,
    NotFound = 2,
    ValidationFailed = 3,
    Conflict = 4,
    ProviderUnavailable = 5
}
