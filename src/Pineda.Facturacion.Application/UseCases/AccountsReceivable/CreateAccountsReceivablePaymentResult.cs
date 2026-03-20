using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public class CreateAccountsReceivablePaymentResult
{
    public CreateAccountsReceivablePaymentOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public long? AccountsReceivablePaymentId { get; set; }

    public AccountsReceivablePayment? AccountsReceivablePayment { get; set; }
}
