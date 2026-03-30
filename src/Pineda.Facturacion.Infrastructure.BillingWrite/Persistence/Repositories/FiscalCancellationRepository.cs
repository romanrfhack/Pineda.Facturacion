using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

public class FiscalCancellationRepository : IFiscalCancellationRepository
{
    private readonly BillingDbContext _dbContext;

    public FiscalCancellationRepository(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<FiscalCancellation?> GetByFiscalDocumentIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
    {
        return _dbContext.FiscalCancellations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.FiscalDocumentId == fiscalDocumentId, cancellationToken);
    }

    public Task<FiscalCancellation?> GetTrackedByFiscalDocumentIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
    {
        return _dbContext.FiscalCancellations
            .FirstOrDefaultAsync(x => x.FiscalDocumentId == fiscalDocumentId, cancellationToken);
    }

    public Task<FiscalCancellation?> GetByFiscalStampIdAsync(long fiscalStampId, CancellationToken cancellationToken = default)
    {
        return _dbContext.FiscalCancellations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.FiscalStampId == fiscalStampId, cancellationToken);
    }

    public Task<FiscalCancellation?> GetTrackedByFiscalStampIdAsync(long fiscalStampId, CancellationToken cancellationToken = default)
    {
        return _dbContext.FiscalCancellations
            .FirstOrDefaultAsync(x => x.FiscalStampId == fiscalStampId, cancellationToken);
    }

    public async Task AddAsync(FiscalCancellation fiscalCancellation, CancellationToken cancellationToken = default)
    {
        await _dbContext.FiscalCancellations.AddAsync(fiscalCancellation, cancellationToken);
    }
}
