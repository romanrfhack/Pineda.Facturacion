using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.UseCases.PaymentComplements;
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

    public async Task<IReadOnlyList<ExternalRepBaseDocument>> SearchAsync(
        SearchExternalRepBaseDocumentsDataFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.ExternalRepBaseDocuments.AsNoTracking();

        if (filter.FromDate.HasValue)
        {
            var fromUtc = DateTime.SpecifyKind(filter.FromDate.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            query = query.Where(x => x.IssuedAtUtc >= fromUtc);
        }

        if (filter.ToDate.HasValue)
        {
            var toExclusiveUtc = DateTime.SpecifyKind(filter.ToDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            query = query.Where(x => x.IssuedAtUtc < toExclusiveUtc);
        }

        if (!string.IsNullOrWhiteSpace(filter.ReceiverRfc))
        {
            var receiverRfc = filter.ReceiverRfc.Trim().ToUpperInvariant();
            query = query.Where(x => x.ReceiverRfc.Contains(receiverRfc));
        }

        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var search = filter.Query.Trim();
            query = query.Where(x =>
                x.Uuid.Contains(search)
                || x.IssuerRfc.Contains(search)
                || (x.IssuerLegalName != null && x.IssuerLegalName.Contains(search))
                || x.ReceiverRfc.Contains(search)
                || (x.ReceiverLegalName != null && x.ReceiverLegalName.Contains(search))
                || x.Series.Contains(search)
                || x.Folio.Contains(search));
        }

        return await query
            .OrderByDescending(x => x.ImportedAtUtc)
            .ThenByDescending(x => x.IssuedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public Task AddAsync(ExternalRepBaseDocument document, CancellationToken cancellationToken = default)
    {
        return _dbContext.ExternalRepBaseDocuments.AddAsync(document, cancellationToken).AsTask();
    }
}
