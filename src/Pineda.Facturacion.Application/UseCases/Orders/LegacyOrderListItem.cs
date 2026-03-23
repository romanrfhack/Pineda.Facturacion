namespace Pineda.Facturacion.Application.UseCases.Orders;

public sealed class LegacyOrderListItem
{
    public string LegacyOrderId { get; init; } = string.Empty;

    public DateTime OrderDateUtc { get; init; }

    public string CustomerName { get; init; } = string.Empty;

    public decimal Total { get; init; }

    public string? LegacyOrderType { get; init; }

    public bool IsImported { get; init; }

    public long? SalesOrderId { get; init; }

    public long? BillingDocumentId { get; init; }

    public string? BillingDocumentStatus { get; init; }

    public long? FiscalDocumentId { get; init; }

    public string? FiscalDocumentStatus { get; init; }

    public string? ImportStatus { get; init; }
}
