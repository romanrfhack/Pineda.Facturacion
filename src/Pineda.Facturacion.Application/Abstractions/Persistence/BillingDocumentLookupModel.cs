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
}

public sealed class BillingDocumentLookupItemModel
{
    public int LineNumber { get; init; }

    public string? ProductInternalCode { get; init; }

    public string Description { get; init; } = string.Empty;
}

public sealed class BillingDocumentAssociatedOrderLookupModel
{
    public long SalesOrderId { get; init; }

    public string LegacyOrderId { get; init; } = string.Empty;

    public string CustomerName { get; init; } = string.Empty;

    public decimal Total { get; init; }

    public bool IsPrimary { get; init; }
}
