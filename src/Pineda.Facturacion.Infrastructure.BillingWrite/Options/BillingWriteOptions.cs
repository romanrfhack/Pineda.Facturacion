namespace Pineda.Facturacion.Infrastructure.BillingWrite.Options;

public class BillingWriteOptions
{
    public const string SectionName = "BillingWrite";

    public string ConnectionString { get; set; } = string.Empty;
}
