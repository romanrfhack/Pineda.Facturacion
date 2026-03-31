namespace Pineda.Facturacion.Application.Contracts.Pac;

public class FiscalRemoteCfdiQueryGatewayResult
{
    public FiscalRemoteCfdiQueryGatewayOutcome Outcome { get; set; }

    public string ProviderName { get; set; } = string.Empty;

    public string ProviderOperation { get; set; } = string.Empty;

    public string? ProviderTrackingId { get; set; }

    public string? ProviderCode { get; set; }

    public string? ProviderMessage { get; set; }

    public string? XmlContent { get; set; }

    public string? XmlHash { get; set; }

    public bool RemoteExists { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }

    public string? SupportMessage { get; set; }

    public string? RawResponseSummaryJson { get; set; }
}
