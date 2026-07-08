namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public class ReassignAccountsReceivablePaymentApplicationInput
{
    public long AccountsReceivableInvoiceId { get; set; }

    public decimal AppliedAmount { get; set; }
}
