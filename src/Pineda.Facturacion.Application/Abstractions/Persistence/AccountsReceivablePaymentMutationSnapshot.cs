namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public sealed class AccountsReceivablePaymentMutationSnapshot
{
    public long PaymentId { get; init; }

    public decimal Amount { get; init; }

    public long? ReceivedFromFiscalReceiverId { get; init; }

    public bool HasApplications { get; init; }

    public bool HasRepAssociations { get; init; }
}
