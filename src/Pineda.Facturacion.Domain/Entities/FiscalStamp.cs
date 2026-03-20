using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Domain.Entities;

public class FiscalStamp
{
    public long Id { get; set; }

    public long FiscalDocumentId { get; set; }

    public string ProviderName { get; set; } = string.Empty;

    public string ProviderOperation { get; set; } = string.Empty;

    public FiscalStampStatus Status { get; set; }

    public string? ProviderRequestHash { get; set; }

    public string? ProviderTrackingId { get; set; }

    public string? ProviderCode { get; set; }

    public string? ProviderMessage { get; set; }

    public string? Uuid { get; set; }

    public DateTime? StampedAtUtc { get; set; }

    public string? XmlContent { get; set; }

    public string? XmlHash { get; set; }

    public string? OriginalString { get; set; }

    public string? QrCodeTextOrUrl { get; set; }

    public string? RawResponseSummaryJson { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime? LastStatusCheckAtUtc { get; set; }

    public string? LastKnownExternalStatus { get; set; }

    public string? LastStatusProviderCode { get; set; }

    public string? LastStatusProviderMessage { get; set; }

    public string? LastStatusRawResponseSummaryJson { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
