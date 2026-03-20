namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public enum RefreshFiscalDocumentStatusOutcome
{
    Refreshed = 0,
    NotFound = 1,
    ValidationFailed = 2,
    ProviderUnavailable = 3
}
