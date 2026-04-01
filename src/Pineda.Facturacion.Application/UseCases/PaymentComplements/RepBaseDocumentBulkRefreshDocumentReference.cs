namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class RepBaseDocumentBulkRefreshDocumentReference
{
    public string? SourceType { get; init; }

    public long SourceId { get; init; }
}
