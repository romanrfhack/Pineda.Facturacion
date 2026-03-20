namespace Pineda.Facturacion.Application.UseCases.CreateBillingDocument;

public enum CreateBillingDocumentOutcome
{
    Created = 0,
    NotFound = 1,
    Conflict = 2,
    ValidationFailed = 3
}
