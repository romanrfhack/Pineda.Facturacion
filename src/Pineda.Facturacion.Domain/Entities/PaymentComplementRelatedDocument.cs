namespace Pineda.Facturacion.Domain.Entities;

public class PaymentComplementRelatedDocument
{
    public long Id { get; set; }

    public long PaymentComplementDocumentId { get; set; }

    public long AccountsReceivableInvoiceId { get; set; }

    public long? FiscalDocumentId { get; set; }

    public long? FiscalStampId { get; set; }

    public long? ExternalRepBaseDocumentId { get; set; }

    public string RelatedDocumentUuid { get; set; } = string.Empty;

    public int InstallmentNumber { get; set; }

    public decimal PreviousBalance { get; set; }

    public decimal PaidAmount { get; set; }

    public decimal RemainingBalance { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}
