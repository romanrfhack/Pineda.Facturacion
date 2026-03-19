using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

public class LegacyImportRecordRepository : ILegacyImportRecordRepository
{
    private readonly BillingDbContext _dbContext;

    public LegacyImportRecordRepository(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<LegacyImportRecord?> GetBySourceDocumentAsync(
        string sourceSystem,
        string sourceTable,
        string sourceDocumentId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.LegacyImportRecords
            .FirstOrDefaultAsync(
                x => x.SourceSystem == sourceSystem
                    && x.SourceTable == sourceTable
                    && x.SourceDocumentId == sourceDocumentId,
                cancellationToken);
    }

    public async Task AddAsync(LegacyImportRecord legacyImportRecord, CancellationToken cancellationToken = default)
    {
        await _dbContext.LegacyImportRecords.AddAsync(legacyImportRecord, cancellationToken);
    }

    public Task UpdateAsync(LegacyImportRecord legacyImportRecord, CancellationToken cancellationToken = default)
    {
        _dbContext.LegacyImportRecords.Update(legacyImportRecord);
        return Task.CompletedTask;
    }
}
