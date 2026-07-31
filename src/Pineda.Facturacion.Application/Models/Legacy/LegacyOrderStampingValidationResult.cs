namespace Pineda.Facturacion.Application.Models.Legacy;

public enum LegacyOrderStampingValidationOutcome
{
    Valid = 0,
    BlockedByCanceledOrders = 1,
    ValidationUnavailable = 2
}

public sealed class LegacyOrderStampingValidationResult
{
    public LegacyOrderStampingValidationOutcome Outcome { get; init; }

    public string? ErrorMessage { get; init; }

    public IReadOnlyList<LegacyOrderStampingBlockingOrder> BlockingCanceledOrders { get; init; } = [];
}

public sealed class LegacyOrderStampingBlockingOrder
{
    public long SalesOrderId { get; init; }

    public string LegacyOrderId { get; init; } = string.Empty;
}
