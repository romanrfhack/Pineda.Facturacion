namespace Pineda.Facturacion.Application.UseCases.ImportLegacyOrder;

public sealed class ReimportLegacyOrderCommand
{
    public string LegacyOrderId { get; set; } = string.Empty;

    public string ExpectedExistingSourceHash { get; set; } = string.Empty;

    public string ExpectedCurrentSourceHash { get; set; } = string.Empty;

    public string ConfirmationMode { get; set; } = string.Empty;
}
