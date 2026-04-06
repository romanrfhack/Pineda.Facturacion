using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public sealed class AccountsReceivablePaymentRepPreparationEvaluation
{
    public decimal AppliedAmount { get; init; }

    public decimal UnappliedAmount { get; init; }

    public decimal CustomerCreditBalanceAmount { get; init; }

    public AccountsReceivablePaymentUnappliedDisposition UnappliedDisposition { get; init; }

    public AccountsReceivablePaymentRepStatus RepStatus { get; init; }

    public bool ReadyToPrepareRep { get; init; }

    public string? RepBlockReason { get; init; }
}
