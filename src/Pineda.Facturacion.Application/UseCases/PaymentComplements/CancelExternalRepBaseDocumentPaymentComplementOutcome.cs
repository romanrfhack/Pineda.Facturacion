namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public enum CancelExternalRepBaseDocumentPaymentComplementOutcome
{
    Cancelled = 1,
    NotFound = 2,
    ValidationFailed = 3,
    Conflict = 4,
    ProviderRejected = 5,
    ProviderUnavailable = 6
}
