namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public static class ExternalRepOperationalInsightBuilder
{
    public static RepOperationalInsight Build(
        ExternalRepBaseDocumentSummaryReadModel summary,
        ExternalRepBaseDocumentOperationalEvaluation evaluation)
    {
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(evaluation);

        var hasAppliedPaymentsWithoutStampedRep = summary.RegisteredPaymentCount > summary.StampedPaymentComplementCount;
        var hasPreparedRepPendingStamp = summary.PreparedPendingStampCount > 0;
        var hasStampingRejectedRep = summary.StampingRejectedPaymentComplementCount > 0;
        var hasCancellationRejectedRep = summary.CancellationRejectedPaymentComplementCount > 0;
        var hasRepWithError = hasStampingRejectedRep || hasCancellationRejectedRep;
        var hasBlockedOperation = evaluation.IsBlocked;

        var actions = new HashSet<string>(evaluation.AvailableActions, StringComparer.Ordinal);
        if (summary.StampedPaymentComplementCount > 0)
        {
            actions.Add(RepBaseDocumentAvailableAction.RefreshRepStatus.ToString());
        }

        if (summary.CancelablePaymentComplementCount > 0)
        {
            actions.Add(RepBaseDocumentAvailableAction.CancelRep.ToString());
        }

        var alerts = new List<RepOperationalAlert>();
        if (hasBlockedOperation)
        {
            alerts.Add(RepOperationalAlertCatalog.CreateBlockedAlert(evaluation.PrimaryReasonCode, evaluation.PrimaryReasonMessage));
        }

        if (hasAppliedPaymentsWithoutStampedRep)
        {
            alerts.Add(RepOperationalAlertCatalog.Create(
                RepOperationalAlertCode.AppliedPaymentsWithoutStampedRep,
                "Hay pagos aplicados sin REP timbrado en este CFDI externo."));
        }

        if (hasPreparedRepPendingStamp)
        {
            alerts.Add(RepOperationalAlertCatalog.Create(
                RepOperationalAlertCode.PreparedRepPendingStamp,
                "Existe al menos un REP externo preparado pendiente de timbrar."));
        }

        if (hasStampingRejectedRep)
        {
            alerts.Add(RepOperationalAlertCatalog.Create(
                RepOperationalAlertCode.RepStampingRejected,
                "Existe al menos un REP externo con timbrado rechazado y requiere revisión o reintento."));
        }

        if (hasCancellationRejectedRep)
        {
            alerts.Add(RepOperationalAlertCatalog.Create(
                RepOperationalAlertCode.RepCancellationRejected,
                "Existe al menos un REP externo con cancelación rechazada y requiere seguimiento."));
        }

        if (!hasBlockedOperation && !hasAppliedPaymentsWithoutStampedRep && !hasPreparedRepPendingStamp && !hasRepWithError && summary.StampedPaymentComplementCount > 0)
        {
            alerts.Add(RepOperationalAlertCatalog.Create(
                RepOperationalAlertCode.StampedRepAvailable,
                "El CFDI externo ya cuenta con REP timbrado y sólo requiere seguimiento o refresh de estatus."));
        }

        return new RepOperationalInsight
        {
            HasAppliedPaymentsWithoutStampedRep = hasAppliedPaymentsWithoutStampedRep,
            HasPreparedRepPendingStamp = hasPreparedRepPendingStamp || hasStampingRejectedRep,
            HasRepWithError = hasRepWithError,
            HasBlockedOperation = hasBlockedOperation,
            NextRecommendedAction = ResolveNextRecommendedAction(actions, hasBlockedOperation, hasCancellationRejectedRep, summary.StampedPaymentComplementCount > 0),
            AvailableActions = actions.ToList(),
            Alerts = alerts
        };
    }

    private static string ResolveNextRecommendedAction(
        HashSet<string> availableActions,
        bool hasBlockedOperation,
        bool hasCancellationRejectedRep,
        bool hasStampedRep)
    {
        if (hasBlockedOperation)
        {
            return RepBaseDocumentRecommendedAction.Blocked;
        }

        if (availableActions.Contains(RepBaseDocumentAvailableAction.StampRep.ToString()))
        {
            return RepBaseDocumentRecommendedAction.StampRep;
        }

        if (availableActions.Contains(RepBaseDocumentAvailableAction.PrepareRep.ToString()))
        {
            return RepBaseDocumentRecommendedAction.PrepareRep;
        }

        if (availableActions.Contains(RepBaseDocumentAvailableAction.RegisterPayment.ToString()))
        {
            return RepBaseDocumentRecommendedAction.RegisterPayment;
        }

        if (hasCancellationRejectedRep || hasStampedRep)
        {
            return RepBaseDocumentRecommendedAction.RefreshRepStatus;
        }

        return RepBaseDocumentRecommendedAction.NoAction;
    }
}
