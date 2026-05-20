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

    public async Task<AccountsReceivablePaymentMutationSnapshot?> GetMutationSnapshotAsync(long accountsReceivablePaymentId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.AccountsReceivablePayments
            .AsNoTracking()
            .Where(x => x.Id == accountsReceivablePaymentId)
            .Select(x => new AccountsReceivablePaymentMutationSnapshot
            {
                PaymentId = x.Id,
                Amount = x.Amount,
                ReceivedFromFiscalReceiverId = x.ReceivedFromFiscalReceiverId,
                HasApplications = _dbContext.AccountsReceivablePaymentApplications.Any(application => application.AccountsReceivablePaymentId == x.Id),
                HasRepAssociations =
                    _dbContext.PaymentComplementDocuments.Any(document => document.AccountsReceivablePaymentId == x.Id)
                    || _dbContext.PaymentComplementPayments.Any(payment => payment.AccountsReceivablePaymentId == x.Id)
            })
            .FirstOrDefaultAsync(cancellationToken);
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

        if (filter.PaymentIds is { Count: > 0 })
        {
            query = query.Where(x => filter.PaymentIds.Contains(x.Id));
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

    public async Task<IReadOnlyList<AccountsReceivablePayment>> ListByInvoiceIdAsync(long accountsReceivableInvoiceId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.AccountsReceivablePayments
            .AsNoTracking()
            .Include(x => x.Applications)
            .Where(x => x.Applications.Any(a => a.AccountsReceivableInvoiceId == accountsReceivableInvoiceId))
            .OrderByDescending(x => x.PaymentDateUtc)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> TryUpdateAmountIfMutableAsync(
        long accountsReceivablePaymentId,
        decimal amount,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (!_dbContext.Database.IsRelational())
        {
            var payment = await _dbContext.AccountsReceivablePayments.FirstOrDefaultAsync(x => x.Id == accountsReceivablePaymentId, cancellationToken);
            if (payment is null || await HasMutationBlockersAsync(accountsReceivablePaymentId, cancellationToken))
            {
                return false;
            }

            payment.Amount = amount;
            payment.UpdatedAtUtc = updatedAtUtc;
            return true;
        }

        var rows = await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE accounts_receivable_payment
            SET amount = {amount},
                updated_at_utc = {updatedAtUtc}
            WHERE id = {accountsReceivablePaymentId}
              AND NOT EXISTS (
                  SELECT 1
                  FROM accounts_receivable_payment_application application
                  WHERE application.accounts_receivable_payment_id = {accountsReceivablePaymentId})
              AND NOT EXISTS (
                  SELECT 1
                  FROM payment_complement_document document
                  WHERE document.accounts_receivable_payment_id = {accountsReceivablePaymentId})
              AND NOT EXISTS (
                  SELECT 1
                  FROM payment_complement_payment payment_ref
                  WHERE payment_ref.accounts_receivable_payment_id = {accountsReceivablePaymentId})
            """,
            cancellationToken);

        return rows == 1;
    }

    public async Task<bool> TryDeleteIfMutableAsync(long accountsReceivablePaymentId, CancellationToken cancellationToken = default)
    {
        if (!_dbContext.Database.IsRelational())
        {
            var payment = await _dbContext.AccountsReceivablePayments.FirstOrDefaultAsync(x => x.Id == accountsReceivablePaymentId, cancellationToken);
            if (payment is null || await HasMutationBlockersAsync(accountsReceivablePaymentId, cancellationToken))
            {
                return false;
            }

            _dbContext.AccountsReceivablePayments.Remove(payment);
            return true;
        }

        var rows = await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            DELETE FROM accounts_receivable_payment
            WHERE id = {accountsReceivablePaymentId}
              AND NOT EXISTS (
                  SELECT 1
                  FROM accounts_receivable_payment_application application
                  WHERE application.accounts_receivable_payment_id = {accountsReceivablePaymentId})
              AND NOT EXISTS (
                  SELECT 1
                  FROM payment_complement_document document
                  WHERE document.accounts_receivable_payment_id = {accountsReceivablePaymentId})
              AND NOT EXISTS (
                  SELECT 1
                  FROM payment_complement_payment payment_ref
                  WHERE payment_ref.accounts_receivable_payment_id = {accountsReceivablePaymentId})
            """,
            cancellationToken);

        return rows == 1;
    }

    public async Task AddAsync(AccountsReceivablePayment accountsReceivablePayment, CancellationToken cancellationToken = default)
    {
        await _dbContext.AccountsReceivablePayments.AddAsync(accountsReceivablePayment, cancellationToken);
    }

    private async Task<bool> HasMutationBlockersAsync(long accountsReceivablePaymentId, CancellationToken cancellationToken)
    {
        return await _dbContext.AccountsReceivablePaymentApplications.AnyAsync(
                   x => x.AccountsReceivablePaymentId == accountsReceivablePaymentId,
                   cancellationToken)
               || await _dbContext.PaymentComplementDocuments.AnyAsync(
                   x => x.AccountsReceivablePaymentId == accountsReceivablePaymentId,
                   cancellationToken)
               || await _dbContext.PaymentComplementPayments.AnyAsync(
                   x => x.AccountsReceivablePaymentId == accountsReceivablePaymentId,
                   cancellationToken);
    }
}
