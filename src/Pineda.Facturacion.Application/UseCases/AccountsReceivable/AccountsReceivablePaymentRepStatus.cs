namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public enum AccountsReceivablePaymentRepStatus
{
    NoApplications = 0,
    PendingApplications = 1,
    ReadyToPrepare = 2,
    NotEligible = 3,
    Prepared = 4,
    Stamped = 5,
    StampingRejected = 6,
    CancellationRequested = 7,
    Cancelled = 8,
    CancellationRejected = 9
}
