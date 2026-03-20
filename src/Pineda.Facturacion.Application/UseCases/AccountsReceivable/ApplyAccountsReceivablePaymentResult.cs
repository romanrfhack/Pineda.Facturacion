using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public class ApplyAccountsReceivablePaymentResult
{
    public ApplyAccountsReceivablePaymentOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public long AccountsReceivablePaymentId { get; set; }

    public int AppliedCount { get; set; }

    public decimal RemainingPaymentAmount { get; set; }

    public AccountsReceivablePayment? AccountsReceivablePayment { get; set; }

    public List<AccountsReceivablePaymentApplication> Applications { get; set; } = [];
}
