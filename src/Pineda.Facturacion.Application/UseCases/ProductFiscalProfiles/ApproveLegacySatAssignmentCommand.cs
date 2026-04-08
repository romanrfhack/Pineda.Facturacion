namespace Pineda.Facturacion.Application.UseCases.ProductFiscalProfiles;

public sealed class ApproveLegacySatAssignmentCommand
{
    public string InternalCode { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string SatProductServiceCode { get; init; } = string.Empty;

    public string SatUnitCode { get; init; } = string.Empty;

    public string? DefaultUnitText { get; init; }
}
