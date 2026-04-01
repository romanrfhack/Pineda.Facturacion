namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class InternalRepBaseDocumentEligibilityExplanation
{
    public string Status { get; init; } = string.Empty;

    public string PrimaryReasonCode { get; init; } = string.Empty;

    public string PrimaryReasonMessage { get; init; } = string.Empty;

    public IReadOnlyList<InternalRepBaseDocumentEligibilitySignal> SecondarySignals { get; init; } = [];

    public DateTime EvaluatedAtUtc { get; init; }
}
