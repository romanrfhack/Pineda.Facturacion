namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public enum RepBaseDocumentBulkRefreshItemOutcome
{
    Refreshed = 0,
    NoChanges = 1,
    Blocked = 2,
    Failed = 3
}
