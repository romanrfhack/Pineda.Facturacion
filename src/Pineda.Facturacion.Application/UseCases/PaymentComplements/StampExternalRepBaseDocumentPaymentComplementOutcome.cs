namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public enum StampExternalRepBaseDocumentPaymentComplementOutcome
{
    Stamped = 0,
    AlreadyStamped = 1,
    ValidationFailed = 2,
    NotFound = 3,
    Conflict = 4,
    ProviderRejected = 5,
    ProviderUnavailable = 6
}
