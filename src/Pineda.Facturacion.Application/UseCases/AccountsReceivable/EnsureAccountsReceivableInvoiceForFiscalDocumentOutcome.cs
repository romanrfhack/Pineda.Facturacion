namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public enum EnsureAccountsReceivableInvoiceForFiscalDocumentOutcome
{
    Created = 0,
    AlreadyExists = 1,
    Skipped = 2,
    NotFound = 3,
    ValidationFailed = 4
}
