namespace Pineda.Facturacion.Application.UseCases.BillingDocuments;

public enum RemoveBillingDocumentItemOutcome
{
    Removed = 0,
    NotFound = 1,
    Conflict = 2,
    ValidationFailed = 3
}
