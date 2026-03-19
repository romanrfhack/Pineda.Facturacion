using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Domain.Entities;

public class BillingDocument
{
    public long Id { get; set; }

    public long SalesOrderId { get; set; }

    public string DocumentType { get; set; } = string.Empty;

    public string? Series { get; set; }

    public string? Folio { get; set; }

    public BillingDocumentStatus Status { get; set; }

    public string PaymentCondition { get; set; } = string.Empty;

    public string? PaymentMethodSat { get; set; }

    public string? PaymentFormSat { get; set; }

    public DateTime? IssuedAtUtc { get; set; }

    public decimal Subtotal { get; set; }

    public decimal DiscountTotal { get; set; }

    public decimal TaxTotal { get; set; }

    public decimal Total { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public List<BillingDocumentItem> Items { get; set; } = [];
}
