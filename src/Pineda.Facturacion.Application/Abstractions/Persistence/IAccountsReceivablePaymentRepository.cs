using Pineda.Facturacion.Application.UseCases.AccountsReceivable;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface IAccountsReceivablePaymentRepository
{
    Task<AccountsReceivablePayment?> GetByIdAsync(long accountsReceivablePaymentId, CancellationToken cancellationToken = default);

    Task<AccountsReceivablePayment?> GetTrackedByIdAsync(long accountsReceivablePaymentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AccountsReceivablePayment>> SearchAsync(SearchAccountsReceivablePaymentsFilter filter, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AccountsReceivablePayment>> ListByInvoiceIdAsync(long accountsReceivableInvoiceId, CancellationToken cancellationToken = default);

    Task AddAsync(AccountsReceivablePayment accountsReceivablePayment, CancellationToken cancellationToken = default);
}
