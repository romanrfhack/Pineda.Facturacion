namespace Pineda.Facturacion.Infrastructure.FacturaloPlus.Options;

public class SecretReferenceOptions
{
    public const string SectionName = "SecretReferences";

    public Dictionary<string, string> Values { get; set; } = new(StringComparer.Ordinal);
}
