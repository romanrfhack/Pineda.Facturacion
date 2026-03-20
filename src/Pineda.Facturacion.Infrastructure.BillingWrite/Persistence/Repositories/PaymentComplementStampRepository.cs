using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

public class PaymentComplementStampRepository : IPaymentComplementStampRepository
{
    private readonly BillingDbContext _dbContext;

    public PaymentComplementStampRepository(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<PaymentComplementStamp?> GetByPaymentComplementDocumentIdAsync(long paymentComplementDocumentId, CancellationToken cancellationToken = default)
    {
        return _dbContext.PaymentComplementStamps
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.PaymentComplementDocumentId == paymentComplementDocumentId, cancellationToken);
    }

    public Task<PaymentComplementStamp?> GetTrackedByPaymentComplementDocumentIdAsync(long paymentComplementDocumentId, CancellationToken cancellationToken = default)
    {
        return _dbContext.PaymentComplementStamps
            .FirstOrDefaultAsync(x => x.PaymentComplementDocumentId == paymentComplementDocumentId, cancellationToken);
    }

    public async Task AddAsync(PaymentComplementStamp paymentComplementStamp, CancellationToken cancellationToken = default)
    {
        await _dbContext.PaymentComplementStamps.AddAsync(paymentComplementStamp, cancellationToken);
    }
}
