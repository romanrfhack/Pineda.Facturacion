using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.Orders;

public sealed class CreateBulkBillingDocumentResult
{
    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public bool IsSuccess { get; init; }

    public long? BillingDocumentId { get; init; }

    public BillingDocumentStatus? BillingDocumentStatus { get; init; }

    public int SelectedOrderCount { get; init; }

    public int ImportedOrderCount { get; init; }

    public int AssociatedOrderCount { get; init; }

    public CreateBulkBillingDocumentOutcome Outcome { get; init; }

    public IReadOnlyList<string> LegacyOrderIds { get; init; } = [];

    public IReadOnlyList<CreateBulkBillingDocumentOrderError> OrderErrors { get; init; } = [];
}

public sealed class CreateBulkBillingDocumentOrderError
{
    public string LegacyOrderId { get; init; } = string.Empty;

    public string? CustomerName { get; init; }

    public string? ErrorCode { get; init; }

    public string ErrorMessage { get; init; } = string.Empty;
}
