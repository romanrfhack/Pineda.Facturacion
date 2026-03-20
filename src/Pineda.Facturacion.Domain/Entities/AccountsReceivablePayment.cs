namespace Pineda.Facturacion.Domain.Entities;

public class AccountsReceivablePayment
{
    public long Id { get; set; }

    public DateTime PaymentDateUtc { get; set; }

    public string PaymentFormSat { get; set; } = string.Empty;

    public string CurrencyCode { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string? Reference { get; set; }

    public string? Notes { get; set; }

    public long? ReceivedFromFiscalReceiverId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public List<AccountsReceivablePaymentApplication> Applications { get; set; } = [];
}
