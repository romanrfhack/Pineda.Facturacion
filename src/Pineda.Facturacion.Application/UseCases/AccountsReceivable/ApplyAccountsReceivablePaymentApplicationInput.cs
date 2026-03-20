namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public class ApplyAccountsReceivablePaymentApplicationInput
{
    public long AccountsReceivableInvoiceId { get; set; }

    public decimal AppliedAmount { get; set; }
}
