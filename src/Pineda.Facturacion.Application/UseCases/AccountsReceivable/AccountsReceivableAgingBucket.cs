namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public enum AccountsReceivableAgingBucket
{
    Current = 0,
    DueSoon = 1,
    Overdue = 2,
    Paid = 3,
    Cancelled = 4
}
