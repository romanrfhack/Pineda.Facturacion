using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

public sealed class LegacyImportRevisionRepository : ILegacyImportRevisionRepository
{
    private readonly BillingDbContext _dbContext;

    public LegacyImportRevisionRepository(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<LegacyImportRevision?> GetCurrentByLegacyImportRecordIdAsync(long legacyImportRecordId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Set<LegacyImportRevision>()
            .AsNoTracking()
            .Where(x => x.LegacyImportRecordId == legacyImportRecordId && x.IsCurrent)
            .OrderByDescending(x => x.RevisionNumber)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<LegacyImportRevision?> GetTrackedCurrentByLegacyImportRecordIdAsync(long legacyImportRecordId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Set<LegacyImportRevision>()
            .Where(x => x.LegacyImportRecordId == legacyImportRecordId && x.IsCurrent)
            .OrderByDescending(x => x.RevisionNumber)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LegacyImportRevision>> ListByLegacyImportRecordIdAsync(long legacyImportRecordId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<LegacyImportRevision>()
            .AsNoTracking()
            .Where(x => x.LegacyImportRecordId == legacyImportRecordId)
            .OrderByDescending(x => x.RevisionNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetNextRevisionNumberAsync(long legacyImportRecordId, CancellationToken cancellationToken = default)
    {
        var current = await _dbContext.Set<LegacyImportRevision>()
            .AsNoTracking()
            .Where(x => x.LegacyImportRecordId == legacyImportRecordId)
            .Select(x => (int?)x.RevisionNumber)
            .MaxAsync(cancellationToken);

        return (current ?? 0) + 1;
    }

    public async Task AddAsync(LegacyImportRevision revision, CancellationToken cancellationToken = default)
    {
        await _dbContext.Set<LegacyImportRevision>().AddAsync(revision, cancellationToken);
    }
}
