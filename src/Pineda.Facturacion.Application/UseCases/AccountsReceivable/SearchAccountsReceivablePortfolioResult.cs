namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public sealed class SearchAccountsReceivablePortfolioResult
{
    public IReadOnlyList<AccountsReceivablePortfolioItem> Items { get; init; } = [];
}
