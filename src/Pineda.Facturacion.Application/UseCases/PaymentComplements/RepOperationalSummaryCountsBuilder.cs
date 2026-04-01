namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public static class RepOperationalSummaryCountsBuilder
{
    public static RepOperationalSummaryCounts Build<T>(
        IReadOnlyCollection<T> items,
        Func<T, IReadOnlyList<RepOperationalAlert>> alertsAccessor,
        Func<T, string> nextRecommendedActionAccessor,
        Func<T, bool> blockedAccessor)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(alertsAccessor);
        ArgumentNullException.ThrowIfNull(nextRecommendedActionAccessor);
        ArgumentNullException.ThrowIfNull(blockedAccessor);

        var materializedItems = items.ToList();

        return new RepOperationalSummaryCounts
        {
            InfoCount = CountBySeverity(materializedItems, alertsAccessor, RepOperationalAlertSeverity.Info),
            WarningCount = CountBySeverity(materializedItems, alertsAccessor, RepOperationalAlertSeverity.Warning),
            ErrorCount = CountBySeverity(materializedItems, alertsAccessor, RepOperationalAlertSeverity.Error),
            CriticalCount = CountBySeverity(materializedItems, alertsAccessor, RepOperationalAlertSeverity.Critical),
            BlockedCount = materializedItems.Count(blockedAccessor),
            AlertCounts = BuildAlertCounts(materializedItems, alertsAccessor),
            NextRecommendedActionCounts = BuildActionCounts(materializedItems, nextRecommendedActionAccessor)
        };
    }

    private static int CountBySeverity<T>(
        IReadOnlyCollection<T> items,
        Func<T, IReadOnlyList<RepOperationalAlert>> alertsAccessor,
        string severity)
    {
        return items.Count(item => alertsAccessor(item).Any(alert => string.Equals(alert.Severity, severity, StringComparison.OrdinalIgnoreCase)));
    }

    private static IReadOnlyList<RepOperationalCount> BuildAlertCounts<T>(
        IReadOnlyCollection<T> items,
        Func<T, IReadOnlyList<RepOperationalAlert>> alertsAccessor)
    {
        return RepOperationalAlertCode.OrderedValues
            .Select(code => new RepOperationalCount
            {
                Code = code,
                Count = items.Count(item => alertsAccessor(item).Any(alert => string.Equals(alert.Code, code, StringComparison.Ordinal)))
            })
            .Where(x => x.Count > 0)
            .ToList();
    }

    private static IReadOnlyList<RepOperationalCount> BuildActionCounts<T>(
        IReadOnlyCollection<T> items,
        Func<T, string> nextRecommendedActionAccessor)
    {
        return RepBaseDocumentRecommendedAction.OrderedValues
            .Select(code => new RepOperationalCount
            {
                Code = code,
                Count = items.Count(item => string.Equals(nextRecommendedActionAccessor(item), code, StringComparison.Ordinal))
            })
            .Where(x => x.Count > 0)
            .ToList();
    }
}
