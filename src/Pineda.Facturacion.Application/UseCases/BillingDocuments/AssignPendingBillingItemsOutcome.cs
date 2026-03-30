namespace Pineda.Facturacion.Application.UseCases.BillingDocuments;

public enum AssignPendingBillingItemsOutcome
{
    Assigned = 0,
    NotFound = 1,
    Conflict = 2,
    ValidationFailed = 3
}
