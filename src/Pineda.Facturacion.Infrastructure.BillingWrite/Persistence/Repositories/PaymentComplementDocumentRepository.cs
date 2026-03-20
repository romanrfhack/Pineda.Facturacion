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
            .Include(x => x.RelatedDocuments)
            .FirstOrDefaultAsync(x => x.Id == paymentComplementDocumentId, cancellationToken);
    }

    public Task<PaymentComplementDocument?> GetTrackedByIdAsync(long paymentComplementDocumentId, CancellationToken cancellationToken = default)
    {
        return _dbContext.PaymentComplementDocuments
            .Include(x => x.RelatedDocuments)
            .FirstOrDefaultAsync(x => x.Id == paymentComplementDocumentId, cancellationToken);
    }

    public Task<PaymentComplementDocument?> GetByPaymentIdAsync(long accountsReceivablePaymentId, CancellationToken cancellationToken = default)
    {
        return _dbContext.PaymentComplementDocuments
            .AsNoTracking()
            .Include(x => x.RelatedDocuments)
            .FirstOrDefaultAsync(x => x.AccountsReceivablePaymentId == accountsReceivablePaymentId, cancellationToken);
    }

    public Task<PaymentComplementDocument?> GetTrackedByPaymentIdAsync(long accountsReceivablePaymentId, CancellationToken cancellationToken = default)
    {
        return _dbContext.PaymentComplementDocuments
            .Include(x => x.RelatedDocuments)
            .FirstOrDefaultAsync(x => x.AccountsReceivablePaymentId == accountsReceivablePaymentId, cancellationToken);
    }

    public async Task AddAsync(PaymentComplementDocument paymentComplementDocument, CancellationToken cancellationToken = default)
    {
        await _dbContext.PaymentComplementDocuments.AddAsync(paymentComplementDocument, cancellationToken);
    }
}
