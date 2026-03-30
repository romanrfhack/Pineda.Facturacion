namespace Pineda.Facturacion.Application.Contracts.Pac;

public class FiscalCancellationAuthorizationPendingQueryGatewayResult
{
    public FiscalCancellationAuthorizationPendingQueryGatewayOutcome Outcome { get; set; }

    public string ProviderName { get; set; } = string.Empty;

    public string ProviderOperation { get; set; } = string.Empty;

    public string? ProviderCode { get; set; }

    public string? ProviderMessage { get; set; }

    public string? SupportMessage { get; set; }

    public string? RawResponseSummaryJson { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }

    public IReadOnlyList<FiscalCancellationAuthorizationPendingItem> Items { get; set; } = [];
}
