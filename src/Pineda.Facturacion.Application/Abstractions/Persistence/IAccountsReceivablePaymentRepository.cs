using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface IAccountsReceivablePaymentRepository
{
    Task<AccountsReceivablePayment?> GetByIdAsync(long accountsReceivablePaymentId, CancellationToken cancellationToken = default);

    Task<AccountsReceivablePayment?> GetTrackedByIdAsync(long accountsReceivablePaymentId, CancellationToken cancellationToken = default);

    Task AddAsync(AccountsReceivablePayment accountsReceivablePayment, CancellationToken cancellationToken = default);
}
