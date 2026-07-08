namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public enum ReassignAccountsReceivablePaymentApplicationsOutcome
{
    Reassigned = 0,
    NotFound = 1,
    ValidationFailed = 2,
    Conflict = 3
}
