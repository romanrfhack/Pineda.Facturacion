namespace Pineda.Facturacion.Application.UseCases.ProductFiscalProfiles;

public sealed class SuggestSatAssignmentForLegacyItemCommand
{
    public string InternalCode { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string? UnitName { get; init; }

    public string? ImportedSatProductServiceCode { get; init; }

    public string? ImportedSatUnitCode { get; init; }
}
