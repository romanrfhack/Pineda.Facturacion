namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public enum SetAccountsReceivablePaymentUnappliedDispositionOutcome
{
    Updated = 0,
    ValidationFailed = 1,
    NotFound = 2,
    Conflict = 3
}
