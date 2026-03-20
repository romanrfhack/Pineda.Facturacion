namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public class CreateAccountsReceivablePaymentCommand
{
    public DateTime PaymentDateUtc { get; set; }

    public string PaymentFormSat { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string? Reference { get; set; }

    public string? Notes { get; set; }

    public long? ReceivedFromFiscalReceiverId { get; set; }
}
