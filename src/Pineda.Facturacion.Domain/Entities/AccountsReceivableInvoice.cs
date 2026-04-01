using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Domain.Entities;

public class AccountsReceivableInvoice
{
    public long Id { get; set; }

    public long? BillingDocumentId { get; set; }

    public long? FiscalDocumentId { get; set; }

    public long? FiscalStampId { get; set; }

    public long? ExternalRepBaseDocumentId { get; set; }

    public AccountsReceivableInvoiceStatus Status { get; set; }

    public string PaymentMethodSat { get; set; } = string.Empty;

    public string PaymentFormSatInitial { get; set; } = string.Empty;

    public bool IsCreditSale { get; set; }

    public int? CreditDays { get; set; }

    public DateTime IssuedAtUtc { get; set; }

    public DateTime? DueAtUtc { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public decimal Total { get; set; }

    public decimal PaidTotal { get; set; }

    public decimal OutstandingBalance { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public List<AccountsReceivablePaymentApplication> Applications { get; set; } = [];
}
