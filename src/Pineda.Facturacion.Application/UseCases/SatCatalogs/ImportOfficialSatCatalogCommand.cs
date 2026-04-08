namespace Pineda.Facturacion.Application.UseCases.SatCatalogs;

public sealed class ImportOfficialSatCatalogCommand
{
    public byte[] FileContent { get; init; } = [];

    public string SourceVersion { get; init; } = string.Empty;

    public string SourceFileName { get; init; } = string.Empty;

    public string SourceChecksum { get; init; } = string.Empty;
}
