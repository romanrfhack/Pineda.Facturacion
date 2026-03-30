using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.BillingDocuments;

public sealed class UpdateBillingDocumentOrderAssociationResult
{
    public UpdateBillingDocumentOrderAssociationOutcome Outcome { get; init; }

    public bool IsSuccess { get; init; }

    public string? ErrorMessage { get; init; }

    public long BillingDocumentId { get; init; }

    public BillingDocumentStatus? BillingDocumentStatus { get; init; }

    public long? FiscalDocumentId { get; init; }

    public FiscalDocumentStatus? FiscalDocumentStatus { get; init; }

    public long SalesOrderId { get; init; }

    public int AssociatedOrderCount { get; init; }

    public decimal Total { get; init; }
}
