namespace Pineda.Facturacion.Application.UseCases.ProductFiscalProfiles;

public sealed class ImportLegacyFiscalProductMappingsFromCsvCommand
{
    public string SourceFileName { get; init; } = string.Empty;

    public string? SourceName { get; init; }

    public byte[] FileContent { get; init; } = [];
}
