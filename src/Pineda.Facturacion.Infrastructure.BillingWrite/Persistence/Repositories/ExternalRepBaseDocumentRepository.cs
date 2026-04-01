using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

public sealed class ExternalRepBaseDocumentRepository : IExternalRepBaseDocumentRepository
{
    private readonly BillingDbContext _dbContext;

    public ExternalRepBaseDocumentRepository(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<ExternalRepBaseDocument?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return _dbContext.ExternalRepBaseDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<ExternalRepBaseDocument?> GetByUuidAsync(string uuid, CancellationToken cancellationToken = default)
    {
        return _dbContext.ExternalRepBaseDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Uuid == uuid, cancellationToken);
    }

    public Task AddAsync(ExternalRepBaseDocument document, CancellationToken cancellationToken = default)
    {
        return _dbContext.ExternalRepBaseDocuments.AddAsync(document, cancellationToken).AsTask();
    }
}
