using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Domain.Entities;

public class FiscalCancellation
{
    public long Id { get; set; }

    public long FiscalDocumentId { get; set; }

    public long FiscalStampId { get; set; }

    public FiscalCancellationStatus Status { get; set; }

    public string CancellationReasonCode { get; set; } = string.Empty;

    public string? ReplacementUuid { get; set; }

    public string ProviderName { get; set; } = string.Empty;

    public string ProviderOperation { get; set; } = string.Empty;

    public string? ProviderTrackingId { get; set; }

    public string? ProviderCode { get; set; }

    public string? ProviderMessage { get; set; }

    public DateTime RequestedAtUtc { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    public string? RawResponseSummaryJson { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }

    public FiscalCancellationAuthorizationStatus AuthorizationStatus { get; set; }

    public string? AuthorizationProviderOperation { get; set; }

    public string? AuthorizationProviderTrackingId { get; set; }

    public string? AuthorizationProviderCode { get; set; }

    public string? AuthorizationProviderMessage { get; set; }

    public string? AuthorizationErrorCode { get; set; }

    public string? AuthorizationErrorMessage { get; set; }

    public string? AuthorizationRawResponseSummaryJson { get; set; }

    public DateTime? AuthorizationRespondedAtUtc { get; set; }

    public string? AuthorizationRespondedByUsername { get; set; }

    public string? AuthorizationRespondedByDisplayName { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
