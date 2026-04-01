namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class RepOperationalSummaryCounts
{
    public int InfoCount { get; init; }

    public int WarningCount { get; init; }

    public int ErrorCount { get; init; }

    public int CriticalCount { get; init; }

    public int BlockedCount { get; init; }

    public IReadOnlyList<RepOperationalCount> AlertCounts { get; init; } = [];

    public IReadOnlyList<RepOperationalCount> NextRecommendedActionCounts { get; init; } = [];
}
