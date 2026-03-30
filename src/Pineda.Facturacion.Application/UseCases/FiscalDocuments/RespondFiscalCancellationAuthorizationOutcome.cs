namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public enum RespondFiscalCancellationAuthorizationOutcome
{
    Responded = 0,
    ValidationFailed = 1,
    ProviderRejected = 2,
    ProviderUnavailable = 3,
    NotFound = 4,
    Conflict = 5
}
