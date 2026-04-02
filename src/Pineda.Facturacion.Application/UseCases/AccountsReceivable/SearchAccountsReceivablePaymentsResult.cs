namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public sealed class SearchAccountsReceivablePaymentsResult
{
    public IReadOnlyList<AccountsReceivablePaymentOperationalProjection> Items { get; init; } = [];
}
