namespace Pineda.Facturacion.Domain.Entities;

public class FiscalDocumentItem
{
    public long Id { get; set; }

    public long FiscalDocumentId { get; set; }

    public int LineNumber { get; set; }

    public long? BillingDocumentItemId { get; set; }

    public string InternalCode { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal Subtotal { get; set; }

    public decimal TaxTotal { get; set; }

    public decimal Total { get; set; }

    public string SatProductServiceCode { get; set; } = string.Empty;

    public string SatUnitCode { get; set; } = string.Empty;

    public string TaxObjectCode { get; set; } = string.Empty;

    public decimal VatRate { get; set; }

    public string? UnitText { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
