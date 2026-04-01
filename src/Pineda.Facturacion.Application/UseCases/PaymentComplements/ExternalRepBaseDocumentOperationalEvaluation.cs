namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class ExternalRepBaseDocumentOperationalEvaluation
{
    public ExternalRepBaseDocumentOperationalStatus Status { get; init; }

    public bool IsEligible { get; init; }

    public bool IsBlocked { get; init; }

    public string PrimaryReasonCode { get; init; } = string.Empty;

    public string PrimaryReasonMessage { get; init; } = string.Empty;

    public IReadOnlyList<string> AvailableActions { get; init; } = [];
}
