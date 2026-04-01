using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

public sealed class InternalRepBaseDocumentStateRepository : IInternalRepBaseDocumentStateRepository
{
    private readonly BillingDbContext _dbContext;

    public InternalRepBaseDocumentStateRepository(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyDictionary<long, InternalRepBaseDocumentState>> GetByFiscalDocumentIdsAsync(
        IReadOnlyCollection<long> fiscalDocumentIds,
        CancellationToken cancellationToken = default)
    {
        if (fiscalDocumentIds.Count == 0)
        {
            return new Dictionary<long, InternalRepBaseDocumentState>();
        }

        return await _dbContext.InternalRepBaseDocumentStates
            .AsNoTracking()
            .Where(x => fiscalDocumentIds.Contains(x.FiscalDocumentId))
            .ToDictionaryAsync(x => x.FiscalDocumentId, cancellationToken);
    }

    public Task<InternalRepBaseDocumentState?> GetByFiscalDocumentIdAsync(
        long fiscalDocumentId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.InternalRepBaseDocumentStates
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.FiscalDocumentId == fiscalDocumentId, cancellationToken);
    }

    public async Task UpsertAsync(InternalRepBaseDocumentState state, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.InternalRepBaseDocumentStates
            .FirstOrDefaultAsync(x => x.FiscalDocumentId == state.FiscalDocumentId, cancellationToken);

        if (existing is null)
        {
            await _dbContext.InternalRepBaseDocumentStates.AddAsync(state, cancellationToken);
            return;
        }

        existing.LastEligibilityEvaluatedAtUtc = state.LastEligibilityEvaluatedAtUtc;
        existing.LastEligibilityStatus = state.LastEligibilityStatus;
        existing.LastPrimaryReasonCode = state.LastPrimaryReasonCode;
        existing.LastPrimaryReasonMessage = state.LastPrimaryReasonMessage;
        existing.RepPendingFlag = state.RepPendingFlag;
        existing.LastRepIssuedAtUtc = state.LastRepIssuedAtUtc;
        existing.RepCount = state.RepCount;
        existing.TotalPaidApplied = state.TotalPaidApplied;
        existing.UpdatedAtUtc = state.UpdatedAtUtc;
    }
}
