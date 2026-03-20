using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface IAccountsReceivablePaymentApplicationRepository
{
    Task<int> GetNextSequenceForPaymentAsync(long accountsReceivablePaymentId, CancellationToken cancellationToken = default);

    Task AddRangeAsync(
        IReadOnlyCollection<AccountsReceivablePaymentApplication> applications,
        CancellationToken cancellationToken = default);
}
