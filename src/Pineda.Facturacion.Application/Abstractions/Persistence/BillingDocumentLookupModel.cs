namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public sealed class BillingDocumentLookupModel
{
    public long BillingDocumentId { get; init; }

    public long SalesOrderId { get; init; }

    public string LegacyOrderId { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string DocumentType { get; init; } = string.Empty;

    public string CurrencyCode { get; init; } = string.Empty;

    public decimal Total { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public long? FiscalDocumentId { get; init; }

    public string? FiscalDocumentStatus { get; init; }

    public IReadOnlyList<BillingDocumentLookupItemModel> Items { get; init; } = [];

    public IReadOnlyList<BillingDocumentAssociatedOrderLookupModel> AssociatedOrders { get; init; } = [];

    public IReadOnlyList<BillingDocumentRemovedItemTraceModel> RemovedItems { get; init; } = [];
}

public sealed class BillingDocumentLookupItemModel
{
    public long BillingDocumentItemId { get; init; }

    public long SalesOrderId { get; init; }

    public long SalesOrderItemId { get; init; }

    public long? SourceBillingDocumentItemRemovalId { get; init; }

    public int SourceSalesOrderLineNumber { get; init; }

    public string SourceLegacyOrderId { get; init; } = string.Empty;

    public int LineNumber { get; init; }

    public string? ProductInternalCode { get; init; }

    public string Description { get; init; } = string.Empty;

    public decimal Quantity { get; init; }

    public decimal Total { get; init; }
}

public sealed class BillingDocumentAssociatedOrderLookupModel
{
    public long SalesOrderId { get; init; }

    public string LegacyOrderId { get; init; } = string.Empty;

    public string CustomerName { get; init; } = string.Empty;

    public decimal Total { get; init; }

    public bool IsPrimary { get; init; }
}

public sealed class BillingDocumentRemovedItemTraceModel
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

    public bool AvailableForPendingBillingReuse { get; init; }

    public DateTime RemovedAtUtc { get; init; }

    public string CurrentTraceStatus { get; init; } = string.Empty;

    public string CurrentTraceMessage { get; init; } = string.Empty;

    public long? CurrentDestinationBillingDocumentId { get; init; }

    public string? CurrentDestinationBillingDocumentStatus { get; init; }

    public long? CurrentDestinationFiscalDocumentId { get; init; }

    public string? CurrentDestinationFiscalDocumentStatus { get; init; }

    public string? FinalCfdiUuid { get; init; }

    public string? FinalCfdiSeries { get; init; }

    public string? FinalCfdiFolio { get; init; }

    public DateTime? FinalStampedAtUtc { get; init; }

    public IReadOnlyList<BillingDocumentRemovedItemAssignmentTraceModel> AssignmentHistory { get; init; } = [];
}

public sealed class BillingDocumentRemovedItemAssignmentTraceModel
{
    public long AssignmentId { get; init; }

    public long DestinationBillingDocumentId { get; init; }

    public string? DestinationBillingDocumentStatus { get; init; }

    public long? DestinationFiscalDocumentId { get; init; }

    public string? DestinationFiscalDocumentStatus { get; init; }

    public string? DestinationFinalCfdiUuid { get; init; }

    public string? DestinationFinalCfdiSeries { get; init; }

    public string? DestinationFinalCfdiFolio { get; init; }

    public DateTime? DestinationStampedAtUtc { get; init; }

    public DateTime AssignedAtUtc { get; init; }

    public string? AssignedByDisplayName { get; init; }

    public DateTime? ReleasedAtUtc { get; init; }

    public string? ReleasedByDisplayName { get; init; }
}
