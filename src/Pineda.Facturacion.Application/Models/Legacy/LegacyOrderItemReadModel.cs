namespace Pineda.Facturacion.Application.Models.Legacy;

public class LegacyOrderItemReadModel
{
    public int LineNumber { get; set; }

    public string LegacyArticleId { get; set; } = string.Empty;

    public string? Sku { get; set; }

    public string Description { get; set; } = string.Empty;

    public string? UnitCode { get; set; }

    public string? UnitName { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TaxRate { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal LineTotal { get; set; }

    public string? SatProductServiceCode { get; set; }

    public string? SatUnitCode { get; set; }
}
