using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

public sealed class AccountsReceivableCollectionRepository : IAccountsReceivableCollectionRepository
{
    private readonly BillingDbContext _dbContext;

    public AccountsReceivableCollectionRepository(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddCommitmentAsync(CollectionCommitment commitment, CancellationToken cancellationToken = default)
        => _dbContext.CollectionCommitments.AddAsync(commitment, cancellationToken).AsTask();

    public Task AddNoteAsync(CollectionNote note, CancellationToken cancellationToken = default)
        => _dbContext.CollectionNotes.AddAsync(note, cancellationToken).AsTask();

    public Task<IReadOnlyList<CollectionCommitment>> ListCommitmentsByInvoiceIdAsync(long accountsReceivableInvoiceId, CancellationToken cancellationToken = default)
        => _dbContext.CollectionCommitments
            .AsNoTracking()
            .Where(x => x.AccountsReceivableInvoiceId == accountsReceivableInvoiceId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken)
            .ContinueWith<IReadOnlyList<CollectionCommitment>>(x => x.Result, cancellationToken);

    public async Task<IReadOnlyList<CollectionCommitment>> ListCommitmentsByInvoiceIdsAsync(IReadOnlyCollection<long> accountsReceivableInvoiceIds, CancellationToken cancellationToken = default)
    {
        if (accountsReceivableInvoiceIds.Count == 0)
        {
            return [];
        }

        return await _dbContext.CollectionCommitments
            .AsNoTracking()
            .Where(x => accountsReceivableInvoiceIds.Contains(x.AccountsReceivableInvoiceId))
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CollectionCommitment>> GetTrackedOpenCommitmentsByInvoiceIdsAsync(IReadOnlyCollection<long> accountsReceivableInvoiceIds, CancellationToken cancellationToken = default)
    {
        if (accountsReceivableInvoiceIds.Count == 0)
        {
            return [];
        }

        return await _dbContext.CollectionCommitments
            .Where(x => accountsReceivableInvoiceIds.Contains(x.AccountsReceivableInvoiceId)
                && x.Status != CollectionCommitmentStatus.Fulfilled
                && x.Status != CollectionCommitmentStatus.Cancelled)
            .ToListAsync(cancellationToken);
    }

    public Task<IReadOnlyList<CollectionNote>> ListNotesByInvoiceIdAsync(long accountsReceivableInvoiceId, CancellationToken cancellationToken = default)
        => _dbContext.CollectionNotes
            .AsNoTracking()
            .Where(x => x.AccountsReceivableInvoiceId == accountsReceivableInvoiceId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken)
            .ContinueWith<IReadOnlyList<CollectionNote>>(x => x.Result, cancellationToken);

    public async Task<IReadOnlyList<CollectionNote>> ListNotesByInvoiceIdsAsync(IReadOnlyCollection<long> accountsReceivableInvoiceIds, CancellationToken cancellationToken = default)
    {
        if (accountsReceivableInvoiceIds.Count == 0)
        {
            return [];
        }

        return await _dbContext.CollectionNotes
            .AsNoTracking()
            .Where(x => accountsReceivableInvoiceIds.Contains(x.AccountsReceivableInvoiceId))
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);
    }
}
