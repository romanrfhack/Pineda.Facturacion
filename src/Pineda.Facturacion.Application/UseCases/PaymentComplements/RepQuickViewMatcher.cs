namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public static class RepQuickViewMatcher
{
    public static bool Matches(string quickView, IReadOnlyList<RepOperationalAlert> alerts, string nextRecommendedAction, int stampedRepCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(quickView);
        ArgumentNullException.ThrowIfNull(alerts);

        var normalizedQuickView = quickView.Trim();

        return normalizedQuickView switch
        {
            nameof(RepQuickViewCode.PendingStamp) =>
                HasAlert(alerts, RepOperationalAlertCode.PreparedRepPendingStamp)
                || string.Equals(nextRecommendedAction, RepBaseDocumentRecommendedAction.StampRep, StringComparison.Ordinal),

            nameof(RepQuickViewCode.WithError) =>
                HasAlert(alerts, RepOperationalAlertCode.RepStampingRejected)
                || HasAlert(alerts, RepOperationalAlertCode.RepCancellationRejected)
                || HasSeverity(alerts, RepOperationalAlertSeverity.Error),

            nameof(RepQuickViewCode.Blocked) =>
                HasAlert(alerts, RepOperationalAlertCode.BlockedOperation)
                || HasAlert(alerts, RepOperationalAlertCode.CancelledBaseDocument)
                || HasAlert(alerts, RepOperationalAlertCode.ValidationBlocked)
                || HasAlert(alerts, RepOperationalAlertCode.UnsupportedCurrency)
                || string.Equals(nextRecommendedAction, RepBaseDocumentRecommendedAction.Blocked, StringComparison.Ordinal)
                || HasSeverity(alerts, RepOperationalAlertSeverity.Critical),

            nameof(RepQuickViewCode.AppliedPaymentWithoutStampedRep) =>
                HasAlert(alerts, RepOperationalAlertCode.AppliedPaymentsWithoutStampedRep),

            nameof(RepQuickViewCode.PendingRefresh) =>
                string.Equals(nextRecommendedAction, RepBaseDocumentRecommendedAction.RefreshRepStatus, StringComparison.Ordinal)
                || HasAlert(alerts, RepOperationalAlertCode.SatValidationUnavailable),

            nameof(RepQuickViewCode.Stamped) =>
                stampedRepCount > 0
                && !HasSeverity(alerts, RepOperationalAlertSeverity.Warning)
                && !HasSeverity(alerts, RepOperationalAlertSeverity.Error)
                && !HasSeverity(alerts, RepOperationalAlertSeverity.Critical),

            _ => false
        };
    }

    private static bool HasAlert(IReadOnlyList<RepOperationalAlert> alerts, string code)
    {
        return alerts.Any(alert => string.Equals(alert.Code, code, StringComparison.Ordinal));
    }

    private static bool HasSeverity(IReadOnlyList<RepOperationalAlert> alerts, string severity)
    {
        return alerts.Any(alert => string.Equals(alert.Severity, severity, StringComparison.OrdinalIgnoreCase));
    }
}
