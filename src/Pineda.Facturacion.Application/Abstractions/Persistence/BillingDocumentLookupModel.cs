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
}
