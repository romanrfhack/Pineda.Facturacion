using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.UseCases.AccountsReceivable;
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

    public Task<AccountsReceivableInvoice?> GetByExternalRepBaseDocumentIdAsync(long externalRepBaseDocumentId, CancellationToken cancellationToken = default)
    {
        return _dbContext.AccountsReceivableInvoices
            .AsNoTracking()
            .Include(x => x.Applications)
            .FirstOrDefaultAsync(x => x.ExternalRepBaseDocumentId == externalRepBaseDocumentId, cancellationToken);
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

    public Task<AccountsReceivableInvoice?> GetTrackedByExternalRepBaseDocumentIdAsync(long externalRepBaseDocumentId, CancellationToken cancellationToken = default)
    {
        return _dbContext.AccountsReceivableInvoices
            .Include(x => x.Applications)
            .FirstOrDefaultAsync(x => x.ExternalRepBaseDocumentId == externalRepBaseDocumentId, cancellationToken);
    }

    public async Task<IReadOnlyList<AccountsReceivableInvoice>> GetByIdsAsync(IReadOnlyCollection<long> accountsReceivableInvoiceIds, CancellationToken cancellationToken = default)
    {
        if (accountsReceivableInvoiceIds.Count == 0)
        {
            return [];
        }

        return await _dbContext.AccountsReceivableInvoices
            .AsNoTracking()
            .Include(x => x.Applications)
            .Where(x => accountsReceivableInvoiceIds.Contains(x.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AccountsReceivablePortfolioItem>> SearchPortfolioAsync(SearchAccountsReceivablePortfolioFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var receiverQuery = string.IsNullOrWhiteSpace(filter.ReceiverQuery) ? null : filter.ReceiverQuery.Trim().ToUpperInvariant();
        var statusFilter = string.IsNullOrWhiteSpace(filter.Status) ? null : filter.Status.Trim();
        var today = DateTime.UtcNow.Date;

        var query =
            from invoice in _dbContext.AccountsReceivableInvoices.AsNoTracking()
            join fiscalDocumentLeft in _dbContext.FiscalDocuments.AsNoTracking() on invoice.FiscalDocumentId equals fiscalDocumentLeft.Id into fiscalDocumentGroup
            from fiscalDocument in fiscalDocumentGroup.DefaultIfEmpty()
            join fiscalStampLeft in _dbContext.FiscalStamps.AsNoTracking() on invoice.FiscalStampId equals fiscalStampLeft.Id into fiscalStampGroup
            from fiscalStamp in fiscalStampGroup.DefaultIfEmpty()
            join fiscalReceiverLeft in _dbContext.FiscalReceivers.AsNoTracking() on invoice.FiscalReceiverId equals fiscalReceiverLeft.Id into fiscalReceiverGroup
            from fiscalReceiver in fiscalReceiverGroup.DefaultIfEmpty()
            select new
            {
                Invoice = invoice,
                FiscalDocument = fiscalDocument,
                FiscalStamp = fiscalStamp,
                FiscalReceiver = fiscalReceiver
            };

        if (filter.FiscalReceiverId.HasValue)
        {
            query = query.Where(x => x.Invoice.FiscalReceiverId == filter.FiscalReceiverId.Value);
        }

        if (!string.IsNullOrWhiteSpace(statusFilter)
            && Enum.TryParse<Domain.Enums.AccountsReceivableInvoiceStatus>(statusFilter, ignoreCase: true, out var parsedStatus))
        {
            query = query.Where(x => x.Invoice.Status == parsedStatus);
        }

        if (filter.DueDateFromUtc.HasValue)
        {
            var dueDateFrom = filter.DueDateFromUtc.Value.Date;
            query = query.Where(x => x.Invoice.DueAtUtc.HasValue && x.Invoice.DueAtUtc.Value >= dueDateFrom);
        }

        if (filter.DueDateToUtcInclusive.HasValue)
        {
            var dueDateTo = filter.DueDateToUtcInclusive.Value.Date.AddDays(1);
            query = query.Where(x => x.Invoice.DueAtUtc.HasValue && x.Invoice.DueAtUtc.Value < dueDateTo);
        }

        if (filter.HasPendingBalance.HasValue)
        {
            query = filter.HasPendingBalance.Value
                ? query.Where(x => x.Invoice.OutstandingBalance > 0m)
                : query.Where(x => x.Invoice.OutstandingBalance <= 0m);
        }

        if (!string.IsNullOrWhiteSpace(receiverQuery))
        {
            query = query.Where(x =>
                (x.FiscalReceiver != null && (
                    x.FiscalReceiver.Rfc.ToUpper().Contains(receiverQuery)
                    || x.FiscalReceiver.LegalName.ToUpper().Contains(receiverQuery)
                    || (x.FiscalReceiver.SearchAlias != null && x.FiscalReceiver.SearchAlias.ToUpper().Contains(receiverQuery))))
                || (x.FiscalDocument != null && (
                    x.FiscalDocument.ReceiverRfc.ToUpper().Contains(receiverQuery)
                    || x.FiscalDocument.ReceiverLegalName.ToUpper().Contains(receiverQuery))));
        }

        var items = await query
            .OrderByDescending(x => x.Invoice.DueAtUtc ?? x.Invoice.IssuedAtUtc)
            .ThenByDescending(x => x.Invoice.Id)
            .Select(x => new
            {
                x.Invoice.Id,
                x.Invoice.FiscalDocumentId,
                x.Invoice.FiscalReceiverId,
                ReceiverRfc = x.FiscalReceiver != null ? x.FiscalReceiver.Rfc : x.FiscalDocument != null ? x.FiscalDocument.ReceiverRfc : null,
                ReceiverLegalName = x.FiscalReceiver != null ? x.FiscalReceiver.LegalName : x.FiscalDocument != null ? x.FiscalDocument.ReceiverLegalName : null,
                FiscalSeries = x.FiscalDocument != null ? x.FiscalDocument.Series : null,
                FiscalFolio = x.FiscalDocument != null ? x.FiscalDocument.Folio : null,
                FiscalUuid = x.FiscalStamp != null ? x.FiscalStamp.Uuid : null,
                x.Invoice.Total,
                x.Invoice.PaidTotal,
                x.Invoice.OutstandingBalance,
                x.Invoice.IssuedAtUtc,
                x.Invoice.DueAtUtc,
                Status = x.Invoice.Status
            })
            .ToListAsync(cancellationToken);

        return items
            .Select(x => new AccountsReceivablePortfolioItem
            {
                AccountsReceivableInvoiceId = x.Id,
                FiscalDocumentId = x.FiscalDocumentId,
                FiscalReceiverId = x.FiscalReceiverId,
                ReceiverRfc = x.ReceiverRfc,
                ReceiverLegalName = x.ReceiverLegalName,
                FiscalSeries = x.FiscalSeries,
                FiscalFolio = x.FiscalFolio,
                FiscalUuid = x.FiscalUuid,
                Total = x.Total,
                PaidTotal = x.PaidTotal,
                OutstandingBalance = x.OutstandingBalance,
                IssuedAtUtc = x.IssuedAtUtc,
                DueAtUtc = x.DueAtUtc,
                Status = x.Status.ToString(),
                DaysPastDue = x.DueAtUtc.HasValue && x.OutstandingBalance > 0m && x.Status != Domain.Enums.AccountsReceivableInvoiceStatus.Cancelled
                    ? Math.Max(0, (today - x.DueAtUtc.Value.Date).Days)
                    : 0
            })
            .ToList();
    }

    public async Task AddAsync(AccountsReceivableInvoice accountsReceivableInvoice, CancellationToken cancellationToken = default)
    {
        await _dbContext.AccountsReceivableInvoices.AddAsync(accountsReceivableInvoice, cancellationToken);
    }
}
