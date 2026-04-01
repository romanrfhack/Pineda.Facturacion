namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class RefreshExternalRepBaseDocumentPaymentComplementStatusResult
{
    public RefreshExternalRepBaseDocumentPaymentComplementStatusOutcome Outcome { get; init; }

    public bool IsSuccess { get; init; }

    public string? ErrorMessage { get; init; }

    public long ExternalRepBaseDocumentId { get; init; }

    public long? PaymentComplementDocumentId { get; init; }

    public string? PaymentComplementStatus { get; init; }

    public string? Uuid { get; init; }

    public string? LastKnownExternalStatus { get; init; }

    public string? ProviderCode { get; init; }

    public string? ProviderMessage { get; init; }

    public DateTime? CheckedAtUtc { get; init; }

    public string? SupportMessage { get; init; }

    public string? RawResponseSummaryJson { get; init; }

    public ExternalRepBaseDocumentListItem? UpdatedSummary { get; init; }
}
