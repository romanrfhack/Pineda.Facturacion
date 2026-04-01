namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class InternalRepBaseDocumentOperationalSnapshot
{
    public DateTime LastEligibilityEvaluatedAtUtc { get; init; }

    public string LastEligibilityStatus { get; init; } = string.Empty;

    public string LastPrimaryReasonCode { get; init; } = string.Empty;

    public string LastPrimaryReasonMessage { get; init; } = string.Empty;

    public bool RepPendingFlag { get; init; }

    public DateTime? LastRepIssuedAtUtc { get; init; }

    public int RepCount { get; init; }

    public decimal TotalPaidApplied { get; init; }
}
