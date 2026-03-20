namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public enum PrepareFiscalDocumentOutcome
{
    Created = 0,
    NotFound = 1,
    Conflict = 2,
    MissingIssuerProfile = 3,
    MissingReceiver = 4,
    MissingProductFiscalProfile = 5,
    ValidationFailed = 6
}
