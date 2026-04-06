namespace Pineda.Facturacion.Domain.Entities;

public class SatProductServiceCatalogEntry
{
    public string Code { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string NormalizedDescription { get; set; } = string.Empty;

    public string KeywordsNormalized { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public string SourceVersion { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
