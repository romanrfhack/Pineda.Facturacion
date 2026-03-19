namespace Pineda.Facturacion.Infrastructure.LegacyRead.Options;

public class LegacyReadOptions
{
    public const string SectionName = "LegacyRead";

    public string ConnectionString { get; set; } = string.Empty;
}
