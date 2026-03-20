namespace Pineda.Facturacion.Domain.Entities;

public class ProductFiscalProfile
{
    public long Id { get; set; }

    public string InternalCode { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string NormalizedDescription { get; set; } = string.Empty;

    public string SatProductServiceCode { get; set; } = string.Empty;

    public string SatUnitCode { get; set; } = string.Empty;

    public string TaxObjectCode { get; set; } = string.Empty;

    public decimal VatRate { get; set; }

    public string? DefaultUnitText { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
