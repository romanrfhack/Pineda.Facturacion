namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public sealed class ImportedLegacyOrderLookupModel
{
    public string LegacyOrderId { get; init; } = string.Empty;

    public long? SalesOrderId { get; init; }

    public string? SalesOrderStatus { get; init; }

    public long? BillingDocumentId { get; init; }

    public string? BillingDocumentStatus { get; init; }

    public long? FiscalDocumentId { get; init; }

    public string? FiscalDocumentStatus { get; init; }

    public string? FiscalUuid { get; init; }

    public string? ImportStatus { get; init; }

    public DateTime? ImportedAtUtc { get; init; }

    public string? ExistingSourceHash { get; init; }
}
