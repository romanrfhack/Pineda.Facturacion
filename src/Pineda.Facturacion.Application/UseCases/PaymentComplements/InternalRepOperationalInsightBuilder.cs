namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public static class InternalRepOperationalInsightBuilder
{
    public static RepOperationalInsight Build(InternalRepBaseDocumentSummaryReadModel summary, InternalRepBaseDocumentEligibilityEvaluation evaluation)
    {
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(evaluation);

        var hasAppliedPaymentsWithoutStampedRep = summary.RegisteredPaymentCount > summary.StampedPaymentComplementCount;
        var hasPreparedRepPendingStamp = summary.PreparedPendingStampCount > 0;
        var hasStampingRejectedRep = summary.StampingRejectedPaymentComplementCount > 0;
        var hasCancellationRejectedRep = summary.CancellationRejectedPaymentComplementCount > 0;
        var hasRepWithError = hasStampingRejectedRep || hasCancellationRejectedRep;
        var hasBlockedOperation = evaluation.IsBlocked;

        var actions = new HashSet<string>(StringComparer.Ordinal)
        {
            RepBaseDocumentAvailableAction.ViewDetail.ToString()
        };

        if (evaluation.IsEligible)
        {
            actions.Add(RepBaseDocumentAvailableAction.OpenInternalWorkflow.ToString());
        }

        if (!hasBlockedOperation && evaluation.IsEligible && summary.OutstandingBalance > 0m)
        {
            actions.Add(RepBaseDocumentAvailableAction.RegisterPayment.ToString());
        }

        if (!hasBlockedOperation && summary.RegisteredPaymentCount > summary.PaymentComplementCount)
        {
            actions.Add(RepBaseDocumentAvailableAction.PrepareRep.ToString());
        }

        if (!hasBlockedOperation && (hasPreparedRepPendingStamp || hasStampingRejectedRep))
        {
            actions.Add(RepBaseDocumentAvailableAction.StampRep.ToString());
        }

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
                "Hay pagos aplicados sin REP timbrado en este CFDI."));
        }

        if (hasPreparedRepPendingStamp)
        {
            alerts.Add(RepOperationalAlertCatalog.Create(
                RepOperationalAlertCode.PreparedRepPendingStamp,
                "Existe al menos un REP preparado pendiente de timbrar."));
        }

        if (hasStampingRejectedRep)
        {
            alerts.Add(RepOperationalAlertCatalog.Create(
                RepOperationalAlertCode.RepStampingRejected,
                "Existe al menos un REP con timbrado rechazado y requiere revisión o reintento."));
        }

        if (hasCancellationRejectedRep)
        {
            alerts.Add(RepOperationalAlertCatalog.Create(
                RepOperationalAlertCode.RepCancellationRejected,
                "Existe al menos un REP con cancelación rechazada y requiere seguimiento."));
        }

        if (!hasBlockedOperation && !hasAppliedPaymentsWithoutStampedRep && !hasPreparedRepPendingStamp && !hasRepWithError && summary.StampedPaymentComplementCount > 0)
        {
            alerts.Add(RepOperationalAlertCatalog.Create(
                RepOperationalAlertCode.StampedRepAvailable,
                "El CFDI ya cuenta con REP timbrado y sólo requiere seguimiento o refresh de estatus."));
        }

        return new RepOperationalInsight
        {
            HasAppliedPaymentsWithoutStampedRep = hasAppliedPaymentsWithoutStampedRep,
            HasPreparedRepPendingStamp = hasPreparedRepPendingStamp || hasStampingRejectedRep,
            HasRepWithError = hasRepWithError,
            HasBlockedOperation = hasBlockedOperation,
            NextRecommendedAction = ResolveNextRecommendedAction(
                hasBlockedOperation,
                hasPreparedRepPendingStamp,
                hasStampingRejectedRep,
                summary.RegisteredPaymentCount > summary.PaymentComplementCount,
                evaluation.IsEligible && summary.OutstandingBalance > 0m,
                hasCancellationRejectedRep,
                summary.StampedPaymentComplementCount > 0),
            AvailableActions = actions.ToList(),
            Alerts = alerts
        };
    }

    private static string ResolveNextRecommendedAction(
        bool hasBlockedOperation,
        bool hasPreparedRepPendingStamp,
        bool hasStampingRejectedRep,
        bool hasPaymentPendingPreparation,
        bool canRegisterPayment,
        bool hasCancellationRejectedRep,
        bool hasStampedRep)
    {
        if (hasBlockedOperation)
        {
            return RepBaseDocumentRecommendedAction.Blocked;
        }

        if (hasPreparedRepPendingStamp || hasStampingRejectedRep)
        {
            return RepBaseDocumentRecommendedAction.StampRep;
        }

        if (hasPaymentPendingPreparation)
        {
            return RepBaseDocumentRecommendedAction.PrepareRep;
        }

        if (canRegisterPayment)
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
