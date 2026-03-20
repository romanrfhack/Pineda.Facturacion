namespace Pineda.Facturacion.Application.Contracts.Pac;

public class PaymentComplementStampingGatewayResult
{
    public PaymentComplementStampingGatewayOutcome Outcome { get; set; }

    public string ProviderName { get; set; } = string.Empty;

    public string ProviderOperation { get; set; } = string.Empty;

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
}

public enum PaymentComplementStampingGatewayOutcome
{
    Stamped = 0,
    Rejected = 1,
    ValidationFailed = 2,
    Unavailable = 3
}
