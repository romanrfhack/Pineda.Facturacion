using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Domain.Entities;

public class SalesOrder
{
    public long Id { get; set; }

    public long LegacyImportRecordId { get; set; }

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

    public DateTime SnapshotTakenAtUtc { get; set; }

    public SalesOrderStatus Status { get; set; }

    public List<SalesOrderItem> Items { get; set; } = [];
}
