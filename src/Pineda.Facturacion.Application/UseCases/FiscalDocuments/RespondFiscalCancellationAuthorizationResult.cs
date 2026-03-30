namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public class RespondFiscalCancellationAuthorizationResult
{
    public RespondFiscalCancellationAuthorizationOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public string RequestedResponse { get; set; } = string.Empty;

    public string? AppliedResponse { get; set; }

    public string? Uuid { get; set; }

    public long? FiscalDocumentId { get; set; }

    public string? FiscalDocumentStatus { get; set; }

    public long? FiscalCancellationId { get; set; }

    public string? CancellationStatus { get; set; }

    public string? AuthorizationStatus { get; set; }

    public string? ProviderName { get; set; }

    public string? ProviderTrackingId { get; set; }

    public string? ProviderCode { get; set; }

    public string? ProviderMessage { get; set; }

    public string? ErrorCode { get; set; }

    public string? SupportMessage { get; set; }

    public string? RawResponseSummaryJson { get; set; }

    public DateTime? RespondedAtUtc { get; set; }

    public bool IsRetryable { get; set; }

    public string? RetryAdvice { get; set; }
}
