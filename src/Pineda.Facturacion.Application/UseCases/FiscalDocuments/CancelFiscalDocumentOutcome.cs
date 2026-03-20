namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public enum CancelFiscalDocumentOutcome
{
    Cancelled = 0,
    NotFound = 1,
    Conflict = 2,
    ValidationFailed = 3,
    ProviderRejected = 4,
    ProviderUnavailable = 5
}
