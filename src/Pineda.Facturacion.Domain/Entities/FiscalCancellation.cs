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

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
