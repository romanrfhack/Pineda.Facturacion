namespace Pineda.Facturacion.Domain.Entities;

public class AccountsReceivablePaymentApplication
{
    public long Id { get; set; }

    public long AccountsReceivablePaymentId { get; set; }

    public long AccountsReceivableInvoiceId { get; set; }

    public int ApplicationSequence { get; set; }

    public decimal AppliedAmount { get; set; }

    public decimal PreviousBalance { get; set; }

    public decimal NewBalance { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
