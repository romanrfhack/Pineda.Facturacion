using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.UseCases.AccountsReceivable;
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

    public async Task<IReadOnlyList<AccountsReceivablePayment>> SearchAsync(SearchAccountsReceivablePaymentsFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = _dbContext.AccountsReceivablePayments
            .AsNoTracking()
            .Include(x => x.Applications)
            .AsQueryable();

        if (filter.PaymentId.HasValue)
        {
            query = query.Where(x => x.Id == filter.PaymentId.Value);
        }

        if (filter.FiscalReceiverId.HasValue)
        {
            query = query.Where(x => x.ReceivedFromFiscalReceiverId == filter.FiscalReceiverId.Value);
        }

        if (filter.ReceivedFromUtc.HasValue)
        {
            var from = filter.ReceivedFromUtc.Value;
            query = query.Where(x => x.PaymentDateUtc >= from);
        }

        if (filter.ReceivedToUtcInclusive.HasValue)
        {
            var toExclusive = filter.ReceivedToUtcInclusive.Value.Date.AddDays(1);
            query = query.Where(x => x.PaymentDateUtc < toExclusive);
        }

        if (filter.LinkedFiscalDocumentId.HasValue)
        {
            var linkedFiscalDocumentId = filter.LinkedFiscalDocumentId.Value;
            query = query.Where(x => x.Applications.Any(a =>
                _dbContext.AccountsReceivableInvoices.Any(i => i.Id == a.AccountsReceivableInvoiceId && i.FiscalDocumentId == linkedFiscalDocumentId)));
        }

        return await query
            .OrderByDescending(x => x.PaymentDateUtc)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(AccountsReceivablePayment accountsReceivablePayment, CancellationToken cancellationToken = default)
    {
        await _dbContext.AccountsReceivablePayments.AddAsync(accountsReceivablePayment, cancellationToken);
    }
}
