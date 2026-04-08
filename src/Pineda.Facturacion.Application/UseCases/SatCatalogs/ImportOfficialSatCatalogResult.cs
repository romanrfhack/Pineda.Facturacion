namespace Pineda.Facturacion.Application.UseCases.SatCatalogs;

public sealed class ImportOfficialSatCatalogResult
{
    public ImportOfficialSatCatalogOutcome Outcome { get; init; }

    public bool IsSuccess { get; init; }

    public string? ErrorMessage { get; init; }

    public string SourceFileName { get; init; } = string.Empty;

    public string SourceVersion { get; init; } = string.Empty;

    public string SourceChecksum { get; init; } = string.Empty;

    public bool? ClientChecksumMatchesServer { get; init; }

    public SatCatalogImportExecutionResult ProductServices { get; init; } = new()
    {
        CatalogType = SatCatalogImportTypes.ProductService
    };

    public SatCatalogImportExecutionResult Units { get; init; } = new()
    {
        CatalogType = SatCatalogImportTypes.ClaveUnidad
    };
}

public sealed class SatCatalogImportExecutionResult
{
    public string CatalogType { get; init; } = string.Empty;

    public long? ImportId { get; init; }

    public string Status { get; init; } = string.Empty;

    public bool WasAlreadyImported { get; init; }

    public int TotalRows { get; init; }

    public int InsertedRows { get; init; }

    public int UpdatedRows { get; init; }

    public int DeactivatedRows { get; init; }

    public string? ErrorMessage { get; init; }
}
