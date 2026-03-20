namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public class ApplyAccountsReceivablePaymentCommand
{
    public long AccountsReceivablePaymentId { get; set; }

    public List<ApplyAccountsReceivablePaymentApplicationInput> Applications { get; set; } = [];
}
