namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public sealed class GetAccountsReceivableInvoiceDetailResult
{
    public GetAccountsReceivableInvoiceDetailOutcome Outcome { get; init; }

    public bool IsSuccess { get; init; }

    public AccountsReceivableInvoiceDetailProjection? Detail { get; init; }
}

public enum GetAccountsReceivableInvoiceDetailOutcome
{
    Found = 0,
    NotFound = 1
}
