using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public sealed class UpdateAccountsReceivablePaymentAmountResult
{
    public UpdateAccountsReceivablePaymentAmountOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public long AccountsReceivablePaymentId { get; set; }

    public decimal PreviousAmount { get; set; }

    public decimal UpdatedAmount { get; set; }

    public AccountsReceivablePayment? AccountsReceivablePayment { get; set; }
}
