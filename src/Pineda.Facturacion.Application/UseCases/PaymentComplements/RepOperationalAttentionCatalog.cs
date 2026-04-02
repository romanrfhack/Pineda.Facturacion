namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public static class RepOperationalAttentionCatalog
{
    public static IReadOnlyList<string> OrderedAlertCodes { get; } =
    [
        RepOperationalAlertCode.BlockedOperation,
        RepOperationalAlertCode.CancelledBaseDocument,
        RepOperationalAlertCode.RepStampingRejected,
        RepOperationalAlertCode.RepCancellationRejected,
        RepOperationalAlertCode.SatValidationUnavailable
    ];

    public static bool IsAttentionAlert(string? alertCode)
    {
        return !string.IsNullOrWhiteSpace(alertCode)
            && OrderedAlertCodes.Contains(alertCode.Trim(), StringComparer.Ordinal);
    }

    public static IReadOnlyList<RepOperationalAttentionCandidate> GetCandidates(IReadOnlyList<RepOperationalAlert> alerts)
    {
        ArgumentNullException.ThrowIfNull(alerts);

        return alerts
            .Where(x => IsAttentionAlert(x.Code))
            .OrderBy(x => ResolvePriority(x.Code))
            .ThenByDescending(x => ResolveSeverityPriority(x.Severity))
            .Select(CreateCandidate)
            .ToList();
    }

    public static string ResolveHighestSeverity(IReadOnlyList<RepOperationalAttentionCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        return candidates
            .OrderByDescending(x => ResolveSeverityPriority(x.Severity))
            .Select(x => x.Severity)
            .FirstOrDefault() ?? RepOperationalAlertSeverity.Info;
    }

    public static int ResolveSeverityPriority(string severity)
    {
        return severity switch
        {
            RepOperationalAlertSeverity.Critical => 3,
            RepOperationalAlertSeverity.Error => 2,
            RepOperationalAlertSeverity.Warning => 1,
            _ => 0
        };
    }

    private static RepOperationalAttentionCandidate CreateCandidate(RepOperationalAlert alert)
    {
        return new RepOperationalAttentionCandidate
        {
            AlertCode = alert.Code,
            Severity = alert.Severity,
            Title = ResolveTitle(alert.Code),
            Message = alert.Message,
            HookKey = ResolveHookKey(alert.Code)
        };
    }

    private static string ResolveTitle(string alertCode)
    {
        return alertCode switch
        {
            RepOperationalAlertCode.RepStampingRejected => "Timbrado REP rechazado",
            RepOperationalAlertCode.RepCancellationRejected => "Cancelación REP rechazada",
            RepOperationalAlertCode.SatValidationUnavailable => "Validación SAT no disponible",
            RepOperationalAlertCode.CancelledBaseDocument => "Documento base cancelado",
            RepOperationalAlertCode.BlockedOperation => "Operación bloqueada",
            _ => alertCode
        };
    }

    private static string ResolveHookKey(string alertCode)
    {
        return alertCode switch
        {
            RepOperationalAlertCode.RepStampingRejected => "rep.stamping-rejected",
            RepOperationalAlertCode.RepCancellationRejected => "rep.cancellation-rejected",
            RepOperationalAlertCode.SatValidationUnavailable => "rep.sat-validation-unavailable",
            RepOperationalAlertCode.CancelledBaseDocument => "rep.cancelled-base-document",
            RepOperationalAlertCode.BlockedOperation => "rep.blocked-operation",
            _ => $"rep.{alertCode}"
        };
    }

    private static int ResolvePriority(string alertCode)
    {
        for (var index = 0; index < OrderedAlertCodes.Count; index++)
        {
            if (string.Equals(OrderedAlertCodes[index], alertCode, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return int.MaxValue;
    }
}
