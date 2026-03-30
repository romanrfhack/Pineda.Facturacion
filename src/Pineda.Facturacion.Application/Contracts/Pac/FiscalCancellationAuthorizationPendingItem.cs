namespace Pineda.Facturacion.Application.Contracts.Pac;

public class FiscalCancellationAuthorizationPendingItem
{
    public string Uuid { get; set; } = string.Empty;

    public string? IssuerRfc { get; set; }

    public string? ReceiverRfc { get; set; }

    public string? ProviderCode { get; set; }

    public string? ProviderMessage { get; set; }

    public DateTime? RequestedAtUtc { get; set; }

    public string RawItemSummaryJson { get; set; } = string.Empty;
}
