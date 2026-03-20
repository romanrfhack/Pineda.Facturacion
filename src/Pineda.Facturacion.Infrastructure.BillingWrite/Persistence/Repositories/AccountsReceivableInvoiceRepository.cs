using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

public class AccountsReceivableInvoiceRepository : IAccountsReceivableInvoiceRepository
{
    private readonly BillingDbContext _dbContext;

    public AccountsReceivableInvoiceRepository(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<AccountsReceivableInvoice?> GetByFiscalDocumentIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
    {
        return _dbContext.AccountsReceivableInvoices
            .AsNoTracking()
            .Include(x => x.Applications)
            .FirstOrDefaultAsync(x => x.FiscalDocumentId == fiscalDocumentId, cancellationToken);
    }

    public Task<AccountsReceivableInvoice?> GetTrackedByIdAsync(long accountsReceivableInvoiceId, CancellationToken cancellationToken = default)
    {
        return _dbContext.AccountsReceivableInvoices
            .Include(x => x.Applications)
            .FirstOrDefaultAsync(x => x.Id == accountsReceivableInvoiceId, cancellationToken);
    }

    public Task<AccountsReceivableInvoice?> GetTrackedByFiscalDocumentIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
    {
        return _dbContext.AccountsReceivableInvoices
            .Include(x => x.Applications)
            .FirstOrDefaultAsync(x => x.FiscalDocumentId == fiscalDocumentId, cancellationToken);
    }

    public async Task AddAsync(AccountsReceivableInvoice accountsReceivableInvoice, CancellationToken cancellationToken = default)
    {
        await _dbContext.AccountsReceivableInvoices.AddAsync(accountsReceivableInvoice, cancellationToken);
    }
}
