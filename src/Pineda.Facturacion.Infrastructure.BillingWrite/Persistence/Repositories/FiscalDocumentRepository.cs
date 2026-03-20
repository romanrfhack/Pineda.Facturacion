using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

public class FiscalDocumentRepository : IFiscalDocumentRepository
{
    private readonly BillingDbContext _dbContext;

    public FiscalDocumentRepository(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<FiscalDocument?> GetByIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
    {
        return _dbContext.FiscalDocuments
            .AsNoTracking()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == fiscalDocumentId, cancellationToken);
    }

    public Task<FiscalDocument?> GetTrackedByIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
    {
        return _dbContext.FiscalDocuments
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == fiscalDocumentId, cancellationToken);
    }

    public Task<FiscalDocument?> GetByBillingDocumentIdAsync(long billingDocumentId, CancellationToken cancellationToken = default)
    {
        return _dbContext.FiscalDocuments
            .AsNoTracking()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.BillingDocumentId == billingDocumentId, cancellationToken);
    }

    public async Task AddAsync(FiscalDocument fiscalDocument, CancellationToken cancellationToken = default)
    {
        await _dbContext.FiscalDocuments.AddAsync(fiscalDocument, cancellationToken);
    }
}
