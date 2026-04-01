namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class RepBaseDocumentBulkRefreshUpdatedState
{
    public string OperationalStatus { get; init; } = string.Empty;

    public bool IsEligible { get; init; }

    public bool IsBlocked { get; init; }

    public string PrimaryReasonMessage { get; init; } = string.Empty;

    public string NextRecommendedAction { get; init; } = RepBaseDocumentRecommendedAction.NoAction;

    public IReadOnlyList<RepOperationalAlert> Alerts { get; init; } = [];
}
