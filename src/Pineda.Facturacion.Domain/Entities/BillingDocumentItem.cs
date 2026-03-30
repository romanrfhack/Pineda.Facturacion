namespace Pineda.Facturacion.Domain.Entities;

public class BillingDocumentItem
{
    public long Id { get; set; }

    public long BillingDocumentId { get; set; }

    public long SalesOrderId { get; set; }

    public long SalesOrderItemId { get; set; }

    public int SourceSalesOrderLineNumber { get; set; }

    public string SourceLegacyOrderId { get; set; } = string.Empty;

    public int LineNumber { get; set; }

    public string? Sku { get; set; }

    public string? ProductInternalCode { get; set; }

    public string Description { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TaxRate { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal LineTotal { get; set; }

    public string? SatProductServiceCode { get; set; }

    public string? SatUnitCode { get; set; }

    public string TaxObjectCode { get; set; } = string.Empty;
}
