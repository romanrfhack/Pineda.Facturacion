namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public sealed class PendingBillingItemLookupModel
{
    public long RemovalId { get; init; }

    public long BillingDocumentId { get; init; }

    public long? FiscalDocumentId { get; init; }

    public long SalesOrderId { get; init; }

    public long SalesOrderItemId { get; init; }

    public string SourceLegacyOrderId { get; init; } = string.Empty;

    public string CustomerName { get; init; } = string.Empty;

    public int SourceSalesOrderLineNumber { get; init; }

    public string? ProductInternalCode { get; init; }

    public string Description { get; init; } = string.Empty;

    public decimal QuantityRemoved { get; init; }

    public string RemovalReason { get; init; } = string.Empty;

    public string? Observations { get; init; }

    public string RemovalDisposition { get; init; } = string.Empty;

    public DateTime RemovedAtUtc { get; init; }
}
