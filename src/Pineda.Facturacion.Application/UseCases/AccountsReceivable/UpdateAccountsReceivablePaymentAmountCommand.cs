namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public sealed class UpdateAccountsReceivablePaymentAmountCommand
{
    public long AccountsReceivablePaymentId { get; set; }

    public decimal Amount { get; set; }
}
