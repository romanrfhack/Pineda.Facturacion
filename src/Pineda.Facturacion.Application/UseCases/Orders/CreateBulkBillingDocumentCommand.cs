namespace Pineda.Facturacion.Application.UseCases.Orders;

public sealed class CreateBulkBillingDocumentCommand
{
    public string DocumentType { get; init; } = string.Empty;

    public BulkBillingDocumentSelectionMode SelectionMode { get; init; }

    public IReadOnlyList<string> LegacyOrderIds { get; init; } = [];

    public SearchLegacyOrdersFilter? Filters { get; init; }
}

public enum BulkBillingDocumentSelectionMode
{
    Explicit = 0,
    Filtered = 1
}
