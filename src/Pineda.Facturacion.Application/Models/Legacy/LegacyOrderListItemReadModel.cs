namespace Pineda.Facturacion.Application.Models.Legacy;

public sealed class LegacyOrderListItemReadModel
{
    public string LegacyOrderId { get; init; } = string.Empty;

    public DateTime OrderDateUtc { get; init; }

    public string CustomerName { get; init; } = string.Empty;

    public decimal Total { get; init; }

    public string? LegacyOrderType { get; init; }
}
