using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

public class PaymentComplementCancellationRepository : IPaymentComplementCancellationRepository
{
    private readonly BillingDbContext _dbContext;

    public PaymentComplementCancellationRepository(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<PaymentComplementCancellation?> GetByPaymentComplementDocumentIdAsync(long paymentComplementDocumentId, CancellationToken cancellationToken = default)
    {
        return _dbContext.PaymentComplementCancellations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.PaymentComplementDocumentId == paymentComplementDocumentId, cancellationToken);
    }

    public Task<PaymentComplementCancellation?> GetTrackedByPaymentComplementDocumentIdAsync(long paymentComplementDocumentId, CancellationToken cancellationToken = default)
    {
        return _dbContext.PaymentComplementCancellations
            .FirstOrDefaultAsync(x => x.PaymentComplementDocumentId == paymentComplementDocumentId, cancellationToken);
    }

    public async Task AddAsync(PaymentComplementCancellation paymentComplementCancellation, CancellationToken cancellationToken = default)
    {
        await _dbContext.PaymentComplementCancellations.AddAsync(paymentComplementCancellation, cancellationToken);
    }
}
