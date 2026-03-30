using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.BillingDocuments;

public sealed class AssignPendingBillingItemsResult
{
    public AssignPendingBillingItemsOutcome Outcome { get; init; }

    public bool IsSuccess { get; init; }

    public string? ErrorMessage { get; init; }

    public long BillingDocumentId { get; init; }

    public FiscalDocumentStatus? FiscalDocumentStatus { get; init; }

    public long? FiscalDocumentId { get; init; }

    public int AssignedCount { get; init; }

    public int IncludedItemCount { get; init; }

    public decimal Total { get; init; }
}
