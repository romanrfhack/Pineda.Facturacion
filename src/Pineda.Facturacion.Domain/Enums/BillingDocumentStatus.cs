namespace Pineda.Facturacion.Domain.Enums;

public enum BillingDocumentStatus
{
    Draft = 0,
    ReadyToStamp = 1,
    Stamping = 2,
    Stamped = 3,
    StampFailed = 4,
    Cancelled = 5
}
