using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Domain.Entities;

public class ProductFiscalProfileImportRow
{
    public long Id { get; set; }

    public long BatchId { get; set; }

    public int RowNumber { get; set; }

    public string RawJson { get; set; } = string.Empty;

    public string? SourceExternalId { get; set; }

    public string? NormalizedInternalCode { get; set; }

    public string? NormalizedDescription { get; set; }

    public string? NormalizedSatProductServiceCode { get; set; }

    public string? NormalizedSatUnitCode { get; set; }

    public string? NormalizedTaxObjectCode { get; set; }

    public decimal? NormalizedVatRate { get; set; }

    public string? NormalizedDefaultUnitText { get; set; }

    public ImportRowStatus Status { get; set; }

    public ImportSuggestedAction SuggestedAction { get; set; }

    public string ValidationErrors { get; set; } = "[]";

    public long? ExistingProductFiscalProfileId { get; set; }

    public ImportApplyStatus ApplyStatus { get; set; }

    public DateTime? AppliedAtUtc { get; set; }

    public string? ApplyErrorMessage { get; set; }

    public long? AppliedMasterEntityId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
