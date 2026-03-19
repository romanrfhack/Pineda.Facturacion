namespace Pineda.Facturacion.Domain.Enums;

public enum SalesOrderStatus
{
    SnapshotCreated = 0,
    ReadyForBilling = 1,
    BillingInProgress = 2,
    Billed = 3,
    Cancelled = 4
}
