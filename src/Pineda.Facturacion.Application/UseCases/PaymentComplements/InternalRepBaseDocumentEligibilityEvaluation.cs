namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class InternalRepBaseDocumentEligibilityEvaluation
{
    public InternalRepBaseDocumentOperationalStatus Status { get; init; }

    public bool IsEligible { get; init; }

    public bool IsBlocked { get; init; }

    public string Reason { get; init; } = string.Empty;
}
