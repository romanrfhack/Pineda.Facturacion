namespace Pineda.Facturacion.Application.Contracts.Pac;

public class FiscalStatusQueryGatewayResult
{
    public FiscalStatusQueryGatewayOutcome Outcome { get; set; }

    public string ProviderName { get; set; } = string.Empty;

    public string ProviderOperation { get; set; } = string.Empty;

    public string? ProviderTrackingId { get; set; }

    public string? ProviderCode { get; set; }

    public string? ProviderMessage { get; set; }

    public string? ExternalStatus { get; set; }

    public DateTime CheckedAtUtc { get; set; }

    public string? RawResponseSummaryJson { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }
}
