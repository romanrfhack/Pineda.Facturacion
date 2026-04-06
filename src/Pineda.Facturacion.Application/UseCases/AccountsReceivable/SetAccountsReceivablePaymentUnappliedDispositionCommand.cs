using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public class SetAccountsReceivablePaymentUnappliedDispositionCommand
{
    public long AccountsReceivablePaymentId { get; set; }

    public AccountsReceivablePaymentUnappliedDisposition UnappliedDisposition { get; set; }
}
