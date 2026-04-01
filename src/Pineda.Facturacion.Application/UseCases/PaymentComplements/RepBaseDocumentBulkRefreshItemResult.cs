namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class RepBaseDocumentBulkRefreshItemResult
{
    public string SourceType { get; init; } = string.Empty;

    public long SourceId { get; init; }

    public bool Attempted { get; init; }

    public RepBaseDocumentBulkRefreshItemOutcome Outcome { get; init; }

    public string Message { get; init; } = string.Empty;

    public long? PaymentComplementDocumentId { get; init; }

    public string? PaymentComplementStatus { get; init; }

    public string? LastKnownExternalStatus { get; init; }

    public RepBaseDocumentBulkRefreshUpdatedState? UpdatedState { get; init; }
}
