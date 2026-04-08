namespace Pineda.Facturacion.Application.UseCases.SatProductServices;

public sealed class SearchSatProductServicesResult
{
    public int Page { get; init; }

    public int PageSize { get; init; }

    public bool HasMore { get; init; }

    public IReadOnlyList<SatProductServiceSearchItem> Items { get; init; } = [];
}

public sealed class SatProductServiceSearchItem
{
    public string Code { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string DisplayText { get; init; } = string.Empty;

    public string MatchKind { get; init; } = string.Empty;

    public decimal Score { get; init; }
}
