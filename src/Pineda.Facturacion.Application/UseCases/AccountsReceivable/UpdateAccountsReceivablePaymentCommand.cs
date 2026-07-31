namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public sealed class UpdateAccountsReceivablePaymentCommand
{
    public long AccountsReceivablePaymentId { get; set; }

    public DateTime PaymentDateUtc { get; set; }

    public string PaymentFormSat { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string? Reference { get; set; }

    public string? Notes { get; set; }
}
