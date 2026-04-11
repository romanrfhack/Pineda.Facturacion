namespace Pineda.Facturacion.Api.OperationalHardening;

internal sealed class ForwardedHeadersHardeningOptions
{
    public const string SectionName = "Networking:ForwardedHeaders";

    public bool Enabled { get; set; }

    public int ForwardLimit { get; set; } = 1;

    public string[] KnownProxies { get; set; } = [];

    public string[] KnownNetworks { get; set; } = [];
}
