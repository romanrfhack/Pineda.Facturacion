namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public enum CreateAccountsReceivableInvoiceFromFiscalDocumentOutcome
{
    Created = 0,
    NotFound = 1,
    Conflict = 2,
    ValidationFailed = 3
}
