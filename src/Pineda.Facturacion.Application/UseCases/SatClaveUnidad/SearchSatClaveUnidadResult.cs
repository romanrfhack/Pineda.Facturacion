namespace Pineda.Facturacion.Application.UseCases.SatClaveUnidad;

public sealed class SearchSatClaveUnidadResult
{
    public int Page { get; init; }

    public int PageSize { get; init; }

    public bool HasMore { get; init; }

    public IReadOnlyList<SatClaveUnidadSearchItem> Items { get; init; } = [];
}

public sealed class SatClaveUnidadSearchItem
{
    public string Code { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string DisplayText { get; init; } = string.Empty;

    public string MatchKind { get; init; } = string.Empty;

    public decimal Score { get; init; }

    public string? Symbol { get; init; }
}
