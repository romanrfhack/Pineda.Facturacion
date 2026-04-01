namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class CancelInternalRepBaseDocumentPaymentComplementResult
{
    public CancelInternalRepBaseDocumentPaymentComplementOutcome Outcome { get; init; }

    public bool IsSuccess { get; init; }

    public string? ErrorMessage { get; init; }

    public long FiscalDocumentId { get; init; }

    public long? PaymentComplementDocumentId { get; init; }

    public string? PaymentComplementStatus { get; init; }

    public long? PaymentComplementCancellationId { get; init; }

    public string? CancellationStatus { get; init; }

    public DateTime? CancelledAtUtc { get; init; }

    public string? ProviderName { get; init; }

    public string? ProviderTrackingId { get; init; }

    public string? ProviderCode { get; init; }

    public string? ProviderMessage { get; init; }

    public string? ErrorCode { get; init; }

    public string? SupportMessage { get; init; }

    public string? RawResponseSummaryJson { get; init; }

    public InternalRepBaseDocumentListItem? UpdatedSummary { get; init; }

    public InternalRepBaseDocumentOperationalSnapshot? OperationalState { get; init; }
}
