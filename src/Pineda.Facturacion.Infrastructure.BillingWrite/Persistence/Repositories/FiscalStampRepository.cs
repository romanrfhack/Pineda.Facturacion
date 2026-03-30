using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

public class FiscalStampRepository : IFiscalStampRepository
{
    private readonly BillingDbContext _dbContext;

    public FiscalStampRepository(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<FiscalStamp?> GetByFiscalDocumentIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
    {
        return _dbContext.FiscalStamps
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.FiscalDocumentId == fiscalDocumentId, cancellationToken);
    }

    public Task<FiscalStamp?> GetTrackedByFiscalDocumentIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
    {
        return _dbContext.FiscalStamps
            .FirstOrDefaultAsync(x => x.FiscalDocumentId == fiscalDocumentId, cancellationToken);
    }

    public Task<FiscalStamp?> GetByUuidAsync(string uuid, CancellationToken cancellationToken = default)
    {
        return _dbContext.FiscalStamps
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Uuid == uuid, cancellationToken);
    }

    public Task<FiscalStamp?> GetTrackedByUuidAsync(string uuid, CancellationToken cancellationToken = default)
    {
        return _dbContext.FiscalStamps
            .FirstOrDefaultAsync(x => x.Uuid == uuid, cancellationToken);
    }

    public async Task AddAsync(FiscalStamp fiscalStamp, CancellationToken cancellationToken = default)
    {
        await _dbContext.FiscalStamps.AddAsync(fiscalStamp, cancellationToken);
    }
}
