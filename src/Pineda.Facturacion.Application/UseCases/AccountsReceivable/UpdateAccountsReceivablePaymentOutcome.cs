namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public enum UpdateAccountsReceivablePaymentOutcome
{
    Updated = 1,
    ValidationFailed = 2,
    NotFound = 3,
    Conflict = 4
}
