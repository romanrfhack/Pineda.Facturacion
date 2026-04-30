namespace Pineda.Facturacion.Domain.Entities;

public class LegacyFiscalProductMapping
{
    public long Id { get; set; }

    public string SourceName { get; set; } = string.Empty;

    public string? SourceConceptId { get; set; }

    public string DescriptionRaw { get; set; } = string.Empty;

    public string DescriptionNormalized { get; set; } = string.Empty;

    public string? InternalCatalogRaw { get; set; }

    public string? InternalCatalogNormalized { get; set; }

    public string SatProductServiceCode { get; set; } = string.Empty;

    public string? SatUnitCode { get; set; }

    public string? EanCode { get; set; }

    public string? EanCodeNormalized { get; set; }

    public string? SkuCode { get; set; }

    public string? SkuCodeNormalized { get; set; }

    public bool IsActive { get; set; }

    public bool IsAmbiguousByDescription { get; set; }

    public bool IsAmbiguousByInternalCode { get; set; }

    public long ImportBatchId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public FiscalProductMappingImportBatch? ImportBatch { get; set; }
}
