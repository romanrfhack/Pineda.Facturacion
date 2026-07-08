using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

public class PaymentComplementDocumentRepository : IPaymentComplementDocumentRepository
{
    private readonly BillingDbContext _dbContext;

    public PaymentComplementDocumentRepository(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<PaymentComplementDocument?> GetByIdAsync(long paymentComplementDocumentId, CancellationToken cancellationToken = default)
    {
        return _dbContext.PaymentComplementDocuments
            .AsNoTracking()
            .Include(x => x.Payments)
            .Include(x => x.RelatedDocuments)
            .FirstOrDefaultAsync(x => x.Id == paymentComplementDocumentId, cancellationToken);
    }

    public Task<PaymentComplementDocument?> GetTrackedByIdAsync(long paymentComplementDocumentId, CancellationToken cancellationToken = default)
    {
        return _dbContext.PaymentComplementDocuments
            .Include(x => x.Payments)
            .Include(x => x.RelatedDocuments)
            .FirstOrDefaultAsync(x => x.Id == paymentComplementDocumentId, cancellationToken);
    }

    public Task<PaymentComplementDocument?> GetByPaymentIdAsync(long accountsReceivablePaymentId, CancellationToken cancellationToken = default)
    {
        return _dbContext.PaymentComplementDocuments
            .AsNoTracking()
            .Include(x => x.Payments)
            .Include(x => x.RelatedDocuments)
            .FirstOrDefaultAsync(x => x.AccountsReceivablePaymentId == accountsReceivablePaymentId
                || x.Payments.Any(payment => payment.AccountsReceivablePaymentId == accountsReceivablePaymentId), cancellationToken);
    }

    public Task<PaymentComplementDocument?> GetTrackedByPaymentIdAsync(long accountsReceivablePaymentId, CancellationToken cancellationToken = default)
    {
        return _dbContext.PaymentComplementDocuments
            .Include(x => x.Payments)
            .Include(x => x.RelatedDocuments)
            .FirstOrDefaultAsync(x => x.AccountsReceivablePaymentId == accountsReceivablePaymentId
                || x.Payments.Any(payment => payment.AccountsReceivablePaymentId == accountsReceivablePaymentId), cancellationToken);
    }

    public async Task<IReadOnlyList<PaymentComplementDocument>> GetByPaymentIdsAsync(IReadOnlyCollection<long> accountsReceivablePaymentIds, CancellationToken cancellationToken = default)
    {
        if (accountsReceivablePaymentIds.Count == 0)
        {
            return [];
        }

        return await _dbContext.PaymentComplementDocuments
            .AsNoTracking()
            .Include(x => x.Payments)
            .Include(x => x.RelatedDocuments)
            .Where(x => accountsReceivablePaymentIds.Contains(x.AccountsReceivablePaymentId)
                || x.Payments.Any(payment => accountsReceivablePaymentIds.Contains(payment.AccountsReceivablePaymentId)))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasAnyAssociationForPaymentAsync(long accountsReceivablePaymentId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.PaymentComplementDocuments
                   .AsNoTracking()
                   .AnyAsync(x => x.AccountsReceivablePaymentId == accountsReceivablePaymentId, cancellationToken)
               || await _dbContext.PaymentComplementPayments
                   .AsNoTracking()
                   .AnyAsync(x => x.AccountsReceivablePaymentId == accountsReceivablePaymentId, cancellationToken)
               || await _dbContext.PaymentComplementStamps
                   .AsNoTracking()
                   .AnyAsync(stamp =>
                       _dbContext.PaymentComplementDocuments.Any(document =>
                           document.Id == stamp.PaymentComplementDocumentId
                           && document.AccountsReceivablePaymentId == accountsReceivablePaymentId)
                       || _dbContext.PaymentComplementPayments.Any(payment =>
                           payment.PaymentComplementDocumentId == stamp.PaymentComplementDocumentId
                           && payment.AccountsReceivablePaymentId == accountsReceivablePaymentId),
                       cancellationToken);
    }

    public async Task<bool> HasRelatedDocumentsForInvoiceIdsAsync(
        IReadOnlyCollection<long> accountsReceivableInvoiceIds,
        CancellationToken cancellationToken = default)
    {
        if (accountsReceivableInvoiceIds.Count == 0)
        {
            return false;
        }

        return await _dbContext.PaymentComplementRelatedDocuments
            .AsNoTracking()
            .AnyAsync(x => accountsReceivableInvoiceIds.Contains(x.AccountsReceivableInvoiceId), cancellationToken);
    }

    public async Task AddAsync(PaymentComplementDocument paymentComplementDocument, CancellationToken cancellationToken = default)
    {
        await _dbContext.PaymentComplementDocuments.AddAsync(paymentComplementDocument, cancellationToken);
    }
}
