namespace Pineda.Facturacion.Application.Contracts.Pac;

public sealed class FiscalStampingGlobalInformation
{
    public string Periodicity { get; set; } = string.Empty;

    public string Months { get; set; } = string.Empty;

    public string Year { get; set; } = string.Empty;
}
