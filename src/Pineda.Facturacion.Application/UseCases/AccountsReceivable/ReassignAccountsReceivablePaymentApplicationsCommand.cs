namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public class ReassignAccountsReceivablePaymentApplicationsCommand
{
    public long AccountsReceivablePaymentId { get; set; }

    public string? Reason { get; set; }

    public List<ReassignAccountsReceivablePaymentApplicationInput> Applications { get; set; } = [];
}
