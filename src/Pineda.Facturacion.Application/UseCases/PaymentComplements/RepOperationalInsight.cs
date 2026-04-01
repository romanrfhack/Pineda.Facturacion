namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class RepOperationalInsight
{
    public bool HasAppliedPaymentsWithoutStampedRep { get; init; }

    public bool HasPreparedRepPendingStamp { get; init; }

    public bool HasRepWithError { get; init; }

    public bool HasBlockedOperation { get; init; }

    public string? NextRecommendedAction { get; init; }

    public IReadOnlyList<string> AvailableActions { get; init; } = [];

    public IReadOnlyList<RepOperationalAlert> Alerts { get; init; } = [];
}
