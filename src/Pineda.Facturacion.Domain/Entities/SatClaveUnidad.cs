namespace Pineda.Facturacion.Domain.Entities;

public class SatClaveUnidad
{
    public string Code { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string NormalizedDescription { get; set; } = string.Empty;

    public string? Symbol { get; set; }

    public string? Notes { get; set; }

    public bool IsActive { get; set; }

    public string SourceVersion { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
