namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public sealed class DeleteAccountsReceivablePaymentResult
{
    public DeleteAccountsReceivablePaymentOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public long AccountsReceivablePaymentId { get; set; }

    public decimal DeletedAmount { get; set; }

    public long? ReceivedFromFiscalReceiverId { get; set; }
}
