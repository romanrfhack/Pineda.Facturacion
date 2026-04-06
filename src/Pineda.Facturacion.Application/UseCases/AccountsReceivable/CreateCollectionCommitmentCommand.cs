namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public sealed class CreateCollectionCommitmentCommand
{
    public long AccountsReceivableInvoiceId { get; init; }

    public decimal PromisedAmount { get; init; }

    public DateTime PromisedDateUtc { get; init; }

    public string? Notes { get; init; }
}
