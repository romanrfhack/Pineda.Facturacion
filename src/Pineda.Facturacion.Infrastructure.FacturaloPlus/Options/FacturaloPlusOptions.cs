namespace Pineda.Facturacion.Infrastructure.FacturaloPlus.Options;

public class FacturaloPlusOptions
{
    public const string SectionName = "FacturaloPlus";

    public string BaseUrl { get; set; } = string.Empty;

    public string StampPath { get; set; } = "/cfdi/stamp";

    public string PaymentComplementStampPath { get; set; } = "/cfdi/payment-complement/stamp";

    public string PaymentComplementCancelPath { get; set; } = "/cfdi/payment-complement/cancel";

    public string PaymentComplementStatusQueryPath { get; set; } = "/cfdi/payment-complement/status";

    public string CancelPath { get; set; } = "/cfdi/cancel";

    public string StatusQueryPath { get; set; } = "/cfdi/status";

    public string ProviderName { get; set; } = "FacturaloPlus";

    public string PayloadMode { get; set; } = "JsonSnapshot";

    public string? ApiKeyHeaderName { get; set; } = "X-Api-Key";

    public string? ApiKeyReference { get; set; }

    public int TimeoutSeconds { get; set; } = 30;
}
