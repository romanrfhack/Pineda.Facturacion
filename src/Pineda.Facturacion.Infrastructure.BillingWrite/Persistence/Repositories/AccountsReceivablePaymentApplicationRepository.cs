using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

public class AccountsReceivablePaymentApplicationRepository : IAccountsReceivablePaymentApplicationRepository
{
    private readonly BillingDbContext _dbContext;

    public AccountsReceivablePaymentApplicationRepository(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int> GetNextSequenceForPaymentAsync(long accountsReceivablePaymentId, CancellationToken cancellationToken = default)
    {
        var maxSequence = await _dbContext.AccountsReceivablePaymentApplications
            .Where(x => x.AccountsReceivablePaymentId == accountsReceivablePaymentId)
            .MaxAsync(x => (int?)x.ApplicationSequence, cancellationToken);

        return (maxSequence ?? 0) + 1;
    }

    public async Task<IReadOnlyList<AccountsReceivablePaymentApplication>> ListLaterApplicationsForInvoiceIdsAsync(
        IReadOnlyCollection<long> accountsReceivableInvoiceIds,
        long excludedAccountsReceivablePaymentId,
        DateTime createdAfterUtc,
        CancellationToken cancellationToken = default)
    {
        if (accountsReceivableInvoiceIds.Count == 0)
        {
            return [];
        }

        return await _dbContext.AccountsReceivablePaymentApplications
            .AsNoTracking()
            .Where(x => accountsReceivableInvoiceIds.Contains(x.AccountsReceivableInvoiceId)
                && x.AccountsReceivablePaymentId != excludedAccountsReceivablePaymentId
                && x.CreatedAtUtc > createdAfterUtc)
            .OrderBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task AddRangeAsync(
        IReadOnlyCollection<AccountsReceivablePaymentApplication> applications,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.AccountsReceivablePaymentApplications.AddRangeAsync(applications, cancellationToken);
    }

    public Task RemoveRangeAsync(
        IReadOnlyCollection<AccountsReceivablePaymentApplication> applications,
        CancellationToken cancellationToken = default)
    {
        _dbContext.AccountsReceivablePaymentApplications.RemoveRange(applications);
        return Task.CompletedTask;
    }
}
