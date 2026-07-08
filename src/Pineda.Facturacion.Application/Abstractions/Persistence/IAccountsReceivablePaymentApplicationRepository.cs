using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface IAccountsReceivablePaymentApplicationRepository
{
    Task<int> GetNextSequenceForPaymentAsync(long accountsReceivablePaymentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AccountsReceivablePaymentApplication>> ListLaterApplicationsForInvoiceIdsAsync(
        IReadOnlyCollection<long> accountsReceivableInvoiceIds,
        long excludedAccountsReceivablePaymentId,
        DateTime createdAfterUtc,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<AccountsReceivablePaymentApplication>>(
            Array.Empty<AccountsReceivablePaymentApplication>());
    }

    Task AddRangeAsync(
        IReadOnlyCollection<AccountsReceivablePaymentApplication> applications,
        CancellationToken cancellationToken = default);

    Task RemoveRangeAsync(
        IReadOnlyCollection<AccountsReceivablePaymentApplication> applications,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
