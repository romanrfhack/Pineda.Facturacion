using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

public class FiscalReceiverRepository : IFiscalReceiverRepository
{
    private readonly BillingDbContext _dbContext;

    public FiscalReceiverRepository(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<FiscalReceiver>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var prefix = $"{query}%";

        return await _dbContext.FiscalReceivers
            .AsNoTracking()
            .Where(x =>
                EF.Functions.Like(x.Rfc, prefix)
                || EF.Functions.Like(x.NormalizedLegalName, prefix)
                || (x.NormalizedSearchAlias != null && EF.Functions.Like(x.NormalizedSearchAlias, prefix)))
            .Take(20)
            .ToListAsync(cancellationToken);
    }

    public Task<FiscalReceiver?> GetByRfcAsync(string normalizedRfc, CancellationToken cancellationToken = default)
    {
        return _dbContext.FiscalReceivers
            .AsNoTracking()
            .Include(x => x.SpecialFieldDefinitions.OrderBy(field => field.DisplayOrder))
            .FirstOrDefaultAsync(x => x.Rfc == normalizedRfc, cancellationToken);
    }

    public Task<FiscalReceiver?> GetByIdAsync(long fiscalReceiverId, CancellationToken cancellationToken = default)
    {
        return _dbContext.FiscalReceivers
            .Include(x => x.SpecialFieldDefinitions.OrderBy(field => field.DisplayOrder))
            .FirstOrDefaultAsync(x => x.Id == fiscalReceiverId, cancellationToken);
    }

    public async Task<IReadOnlyList<FiscalReceiverSpecialFieldDefinition>> GetActiveSpecialFieldDefinitionsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.FiscalReceiverSpecialFieldDefinitions
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Label)
            .ThenBy(x => x.DisplayOrder)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(FiscalReceiver fiscalReceiver, CancellationToken cancellationToken = default)
    {
        await _dbContext.FiscalReceivers.AddAsync(fiscalReceiver, cancellationToken);
    }

    public Task UpdateAsync(FiscalReceiver fiscalReceiver, CancellationToken cancellationToken = default)
    {
        _dbContext.FiscalReceivers.Update(fiscalReceiver);
        return Task.CompletedTask;
    }
}
