namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public static class RepBaseDocumentBulkRefreshStateMapper
{
    public static RepBaseDocumentBulkRefreshUpdatedState Map(InternalRepBaseDocumentListItem summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return new RepBaseDocumentBulkRefreshUpdatedState
        {
            OperationalStatus = summary.RepOperationalStatus,
            IsEligible = summary.IsEligible,
            IsBlocked = summary.IsBlocked,
            PrimaryReasonMessage = summary.Eligibility.PrimaryReasonMessage,
            NextRecommendedAction = summary.NextRecommendedAction,
            Alerts = summary.Alerts
        };
    }

    public static RepBaseDocumentBulkRefreshUpdatedState Map(ExternalRepBaseDocumentListItem summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return new RepBaseDocumentBulkRefreshUpdatedState
        {
            OperationalStatus = summary.OperationalStatus,
            IsEligible = summary.IsEligible,
            IsBlocked = summary.IsBlocked,
            PrimaryReasonMessage = summary.PrimaryReasonMessage,
            NextRecommendedAction = summary.NextRecommendedAction,
            Alerts = summary.Alerts
        };
    }
}
