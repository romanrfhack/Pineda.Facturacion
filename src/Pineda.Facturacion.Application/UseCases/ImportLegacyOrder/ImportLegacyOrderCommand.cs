namespace Pineda.Facturacion.Application.UseCases.ImportLegacyOrder;

public class ImportLegacyOrderCommand
{
    public string SourceSystem { get; set; } = string.Empty;

    public string SourceTable { get; set; } = string.Empty;

    public string LegacyOrderId { get; set; } = string.Empty;
}
