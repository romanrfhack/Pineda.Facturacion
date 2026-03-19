namespace Pineda.Facturacion.Application.Models.Legacy;

public class LegacyOrderReadModel
{
    public string LegacyOrderId { get; set; } = string.Empty;

    public string LegacyOrderNumber { get; set; } = string.Empty;

    public string? LegacyOrderType { get; set; }

    public string CustomerLegacyId { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;

    public string? CustomerRfc { get; set; }

    public string PaymentCondition { get; set; } = string.Empty;

    public string? PriceListCode { get; set; }

    public string? DeliveryType { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public decimal Subtotal { get; set; }

    public decimal DiscountTotal { get; set; }

    public decimal TaxTotal { get; set; }

    public decimal Total { get; set; }

    public List<LegacyOrderItemReadModel> Items { get; set; } = [];
}
