namespace Pineda.Facturacion.Domain.Entities;

public class ProductFiscalAssignment
{
    public long Id { get; set; }

    public string InternalCode { get; set; } = string.Empty;

    public string SatProductServiceCode { get; set; } = string.Empty;

    public string SatUnitCode { get; set; } = string.Empty;

    public string TaxObjectCode { get; set; } = string.Empty;

    public decimal VatRate { get; set; }

    public string? DefaultUnitText { get; set; }

    public string Source { get; set; } = string.Empty;

    public decimal Confidence { get; set; }

    public string ReviewStatus { get; set; } = string.Empty;

    public string? ReviewReason { get; set; }

    public DateTime ValidFromUtc { get; set; }

    public DateTime? ValidToUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
