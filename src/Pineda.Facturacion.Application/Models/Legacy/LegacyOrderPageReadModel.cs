namespace Pineda.Facturacion.Application.Models.Legacy;

public sealed class LegacyOrderPageReadModel
{
    public IReadOnlyList<LegacyOrderListItemReadModel> Items { get; init; } = [];

    public int TotalCount { get; init; }

    public int Page { get; init; }

    public int PageSize { get; init; }
}
