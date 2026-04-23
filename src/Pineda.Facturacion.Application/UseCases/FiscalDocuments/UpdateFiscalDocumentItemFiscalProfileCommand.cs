namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public sealed class UpdateFiscalDocumentItemFiscalProfileCommand
{
    public long FiscalDocumentItemId { get; set; }

    public string SatProductServiceCode { get; set; } = string.Empty;

    public string SatUnitCode { get; set; } = string.Empty;

    public string TaxObjectCode { get; set; } = string.Empty;

    public decimal VatRate { get; set; }

    public string? UnitText { get; set; }
}
