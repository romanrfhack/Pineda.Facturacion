namespace Pineda.Facturacion.Application.UseCases.BillingDocuments;

public enum UpdateBillingDocumentOrderAssociationOutcome
{
    Updated = 0,
    NotFound = 1,
    Conflict = 2,
    ValidationFailed = 3
}
