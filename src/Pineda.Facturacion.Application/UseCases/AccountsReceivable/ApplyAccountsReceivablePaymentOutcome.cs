namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public enum ApplyAccountsReceivablePaymentOutcome
{
    Applied = 0,
    NotFound = 1,
    ValidationFailed = 2,
    Conflict = 3
}
