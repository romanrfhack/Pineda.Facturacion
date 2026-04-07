namespace Pineda.Facturacion.Domain.Entities;

public class SatCatalogImport
{
    public long Id { get; set; }

    public string CatalogType { get; set; } = string.Empty;

    public string SourceFileName { get; set; } = string.Empty;

    public string SourceFormat { get; set; } = string.Empty;

    public string SourceVersion { get; set; } = string.Empty;

    public string? SourceChecksum { get; set; }

    public string Status { get; set; } = string.Empty;

    public int TotalRows { get; set; }

    public int InsertedRows { get; set; }

    public int UpdatedRows { get; set; }

    public int DeactivatedRows { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }
}
