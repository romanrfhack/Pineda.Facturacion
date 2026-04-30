namespace Pineda.Facturacion.Infrastructure.Options;

public sealed class EmailDeliverySafetyOptions
{
    public const string SectionName = "Email";

    public string SafeRecipient { get; set; } = string.Empty;

    public string ProductionBccRecipient { get; set; } = string.Empty;
}
