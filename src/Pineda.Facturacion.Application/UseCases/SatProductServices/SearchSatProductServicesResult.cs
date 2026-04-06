namespace Pineda.Facturacion.Application.UseCases.SatProductServices;

public sealed class SearchSatProductServicesResult
{
    public IReadOnlyList<SatProductServiceSearchItem> Items { get; init; } = [];
}

public sealed class SatProductServiceSearchItem
{
    public string Code { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string DisplayText { get; init; } = string.Empty;

    public string MatchKind { get; init; } = string.Empty;
}
