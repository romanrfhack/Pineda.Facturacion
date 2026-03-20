namespace Pineda.Facturacion.Domain.Enums;

public enum PaymentComplementDocumentStatus
{
    Draft = 0,
    ReadyForStamping = 1,
    StampingRequested = 2,
    Stamped = 3,
    StampingRejected = 4,
    CancellationRequested = 5,
    Cancelled = 6,
    CancellationRejected = 7
}
