namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public enum ReprepareFiscalDocumentOutcome
{
    Reprepared = 0,
    NotFound = 1,
    Conflict = 2,
    ValidationFailed = 3
}
