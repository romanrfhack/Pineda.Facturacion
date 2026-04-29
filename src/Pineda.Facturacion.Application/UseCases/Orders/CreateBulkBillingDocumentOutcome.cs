namespace Pineda.Facturacion.Application.UseCases.Orders;

public enum CreateBulkBillingDocumentOutcome
{
    Created = 0,
    ValidationFailed = 1,
    Conflict = 2
}
