namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public static class RepBaseDocumentBulkRefreshOutcomeEvaluator
{
    public static RepBaseDocumentBulkRefreshItemOutcome Evaluate(
        bool isSuccess,
        string? previousPaymentComplementStatus,
        string? refreshedPaymentComplementStatus,
        bool blocked)
    {
        if (blocked)
        {
            return RepBaseDocumentBulkRefreshItemOutcome.Blocked;
        }

        if (!isSuccess)
        {
            return RepBaseDocumentBulkRefreshItemOutcome.Failed;
        }

        if (string.Equals(previousPaymentComplementStatus, refreshedPaymentComplementStatus, StringComparison.OrdinalIgnoreCase))
        {
            return RepBaseDocumentBulkRefreshItemOutcome.NoChanges;
        }

        return RepBaseDocumentBulkRefreshItemOutcome.Refreshed;
    }

    public static string BuildMessage(
        RepBaseDocumentBulkRefreshItemOutcome outcome,
        string? errorMessage,
        string? lastKnownExternalStatus)
    {
        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            return errorMessage;
        }

        return outcome switch
        {
            RepBaseDocumentBulkRefreshItemOutcome.Refreshed when !string.IsNullOrWhiteSpace(lastKnownExternalStatus)
                => $"Estatus refrescado. Estado externo: {lastKnownExternalStatus}.",
            RepBaseDocumentBulkRefreshItemOutcome.Refreshed
                => "Estatus refrescado correctamente.",
            RepBaseDocumentBulkRefreshItemOutcome.NoChanges when !string.IsNullOrWhiteSpace(lastKnownExternalStatus)
                => $"Sin cambios operativos. Estado externo actual: {lastKnownExternalStatus}.",
            RepBaseDocumentBulkRefreshItemOutcome.NoChanges
                => "Sin cambios operativos después del refresh.",
            RepBaseDocumentBulkRefreshItemOutcome.Blocked
                => "El documento no tiene un REP elegible para refresh.",
            _ => "No fue posible refrescar el estatus del REP."
        };
    }
}
