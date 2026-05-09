namespace Pineda.Facturacion.Application.Models.Legacy;

public sealed class LegacyOrderListItemReadModel
{
    public string LegacyOrderId { get; init; } = string.Empty;

    public DateTime OrderDateUtc { get; init; }

    public string CustomerName { get; init; } = string.Empty;

    public string CustomerLegacyId { get; init; } = string.Empty;

    public string? CustomerRfc { get; init; }

    public string CurrencyCode { get; init; } = "MXN";

    public decimal Total { get; init; }

    public string? LegacyOrderType { get; init; }
}
