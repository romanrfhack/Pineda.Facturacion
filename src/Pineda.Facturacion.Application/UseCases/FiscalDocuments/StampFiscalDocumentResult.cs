using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public class StampFiscalDocumentResult
{
    public StampFiscalDocumentOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public long FiscalDocumentId { get; set; }

    public FiscalDocumentStatus? FiscalDocumentStatus { get; set; }

    public long? FiscalStampId { get; set; }

    public string? Uuid { get; set; }

    public DateTime? StampedAtUtc { get; set; }

    public string? ProviderName { get; set; }

    public string? ProviderTrackingId { get; set; }

    public string? ProviderCode { get; set; }

    public string? ProviderMessage { get; set; }

    public string? ErrorCode { get; set; }

    public string? SupportMessage { get; set; }

    public string? RawResponseSummaryJson { get; set; }

    public bool IsRetryable { get; set; }

    public string? RetryAdvice { get; set; }
}
