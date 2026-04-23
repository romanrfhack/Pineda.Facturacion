namespace Pineda.Facturacion.Application.UseCases.BillingDocuments;

public enum CancelBillingDocumentOutcome
{
    Cancelled = 1,
    NotFound = 2,
    Conflict = 3,
    ValidationFailed = 4
}
