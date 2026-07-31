namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public sealed class AccountsReceivablePaymentMutationSnapshot
{
    public long PaymentId { get; init; }

    public DateTime PaymentDateUtc { get; init; }

    public string PaymentFormSat { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public string? Reference { get; init; }

    public string? Notes { get; init; }

    public long? ReceivedFromFiscalReceiverId { get; init; }

    public DateTime UpdatedAtUtc { get; init; }

    public bool HasApplications { get; init; }

    public bool HasRepAssociations { get; init; }
}
