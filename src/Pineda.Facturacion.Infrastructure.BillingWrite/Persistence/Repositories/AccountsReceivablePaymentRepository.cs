using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

public class AccountsReceivablePaymentRepository : IAccountsReceivablePaymentRepository
{
    private readonly BillingDbContext _dbContext;

    public AccountsReceivablePaymentRepository(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<AccountsReceivablePayment?> GetByIdAsync(long accountsReceivablePaymentId, CancellationToken cancellationToken = default)
    {
        return _dbContext.AccountsReceivablePayments
            .AsNoTracking()
            .Include(x => x.Applications)
            .FirstOrDefaultAsync(x => x.Id == accountsReceivablePaymentId, cancellationToken);
    }

    public Task<AccountsReceivablePayment?> GetTrackedByIdAsync(long accountsReceivablePaymentId, CancellationToken cancellationToken = default)
    {
        return _dbContext.AccountsReceivablePayments
            .Include(x => x.Applications)
            .FirstOrDefaultAsync(x => x.Id == accountsReceivablePaymentId, cancellationToken);
    }

    public async Task AddAsync(AccountsReceivablePayment accountsReceivablePayment, CancellationToken cancellationToken = default)
    {
        await _dbContext.AccountsReceivablePayments.AddAsync(accountsReceivablePayment, cancellationToken);
    }
}
