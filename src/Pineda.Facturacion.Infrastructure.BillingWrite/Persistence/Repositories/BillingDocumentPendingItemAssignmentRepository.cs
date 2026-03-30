using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

public sealed class BillingDocumentPendingItemAssignmentRepository : IBillingDocumentPendingItemAssignmentRepository
{
    private readonly BillingDbContext _dbContext;

    public BillingDocumentPendingItemAssignmentRepository(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<BillingDocumentPendingItemAssignment>> ListActiveByBillingDocumentIdAsync(long billingDocumentId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.BillingDocumentPendingItemAssignments
            .Where(x => x.DestinationBillingDocumentId == billingDocumentId && x.ReleasedAtUtc == null)
            .OrderBy(x => x.AssignedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public Task<BillingDocumentPendingItemAssignment?> GetActiveByRemovalIdAsync(long billingDocumentItemRemovalId, CancellationToken cancellationToken = default)
    {
        return _dbContext.BillingDocumentPendingItemAssignments
            .FirstOrDefaultAsync(x => x.BillingDocumentItemRemovalId == billingDocumentItemRemovalId && x.ReleasedAtUtc == null, cancellationToken);
    }

    public async Task AddAsync(BillingDocumentPendingItemAssignment assignment, CancellationToken cancellationToken = default)
    {
        await _dbContext.BillingDocumentPendingItemAssignments.AddAsync(assignment, cancellationToken);
    }
}
