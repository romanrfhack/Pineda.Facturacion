namespace Pineda.Facturacion.Application.UseCases.ProductFiscalProfiles;

public class PreviewProductFiscalProfileImportFromExcelCommand
{
    public string SourceFileName { get; set; } = string.Empty;

    public byte[] FileContent { get; set; } = [];

    public string? DefaultTaxObjectCode { get; set; }

    public decimal? DefaultVatRate { get; set; }

    public string? DefaultUnitText { get; set; }
}
