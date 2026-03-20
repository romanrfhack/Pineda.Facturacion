namespace Pineda.Facturacion.Application.Contracts.Pac;

public class FiscalCancellationGatewayResult
{
    public FiscalCancellationGatewayOutcome Outcome { get; set; }

    public string ProviderName { get; set; } = string.Empty;

    public string ProviderOperation { get; set; } = string.Empty;

    public string? ProviderTrackingId { get; set; }

    public string? ProviderCode { get; set; }

    public string? ProviderMessage { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    public string? RawResponseSummaryJson { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }
}
