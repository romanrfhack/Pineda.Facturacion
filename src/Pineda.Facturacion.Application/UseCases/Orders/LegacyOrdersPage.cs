namespace Pineda.Facturacion.Application.UseCases.Orders;

public sealed class LegacyOrdersPage
{
    public IReadOnlyList<LegacyOrderListItem> Items { get; init; } = [];

    public int TotalCount { get; init; }

    public int Page { get; init; }

    public int PageSize { get; init; }
}
