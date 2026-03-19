namespace Pineda.Facturacion.Application.Models.Legacy;

public class LegacyOrderItemReadModel
{
    public int LineNumber { get; set; }

    public string LegacyArticleId { get; set; } = string.Empty;

    public string Sku { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string UnitCode { get; set; } = string.Empty;

    public string UnitName { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TaxRate { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal LineTotal { get; set; }

    public string SatProductServiceCode { get; set; } = string.Empty;

    public string SatUnitCode { get; set; } = string.Empty;
}
