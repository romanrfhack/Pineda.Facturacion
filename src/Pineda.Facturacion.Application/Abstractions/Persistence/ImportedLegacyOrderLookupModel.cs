namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public sealed class ImportedLegacyOrderLookupModel
{
    public string LegacyOrderId { get; init; } = string.Empty;

    public long? SalesOrderId { get; init; }

    public long? BillingDocumentId { get; init; }

    public string? BillingDocumentStatus { get; init; }

    public long? FiscalDocumentId { get; init; }

    public string? FiscalDocumentStatus { get; init; }

    public string? ImportStatus { get; init; }
}
