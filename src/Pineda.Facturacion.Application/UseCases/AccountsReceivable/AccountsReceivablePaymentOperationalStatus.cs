namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public enum AccountsReceivablePaymentOperationalStatus
{
    CapturedUnapplied = 0,
    PartiallyApplied = 1,
    FullyApplied = 2,
    OverApplied = 3
}
