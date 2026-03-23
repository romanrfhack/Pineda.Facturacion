namespace Pineda.Facturacion.Application.UseCases.Orders;

public sealed class SearchLegacyOrdersFilter
{
    public DateTime FromDateUtc { get; init; }

    public DateTime ToDateUtcExclusive { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 10;
}
