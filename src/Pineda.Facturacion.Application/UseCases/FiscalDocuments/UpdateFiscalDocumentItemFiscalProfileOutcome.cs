namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public enum UpdateFiscalDocumentItemFiscalProfileOutcome
{
    Updated = 0,
    NotFound = 1,
    Conflict = 2,
    ValidationFailed = 3
}
