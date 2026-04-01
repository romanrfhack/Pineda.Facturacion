namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class RepBaseDocumentBulkRefreshResult
{
    public bool IsSuccess { get; init; }

    public string? ErrorMessage { get; init; }

    public string Mode { get; init; } = string.Empty;

    public int MaxDocuments { get; init; }

    public int TotalRequested { get; init; }

    public int TotalAttempted { get; init; }

    public int RefreshedCount { get; init; }

    public int NoChangesCount { get; init; }

    public int BlockedCount { get; init; }

    public int FailedCount { get; init; }

    public IReadOnlyList<RepBaseDocumentBulkRefreshItemResult> Items { get; init; } = [];
}
