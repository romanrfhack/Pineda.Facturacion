using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.UseCases.Audit;
using Pineda.Facturacion.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

public sealed class AuditEventRepository : IAuditEventRepository
{
    private readonly BillingDbContext _dbContext;

    public AuditEventRepository(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        return _dbContext.AuditEvents.AddAsync(auditEvent, cancellationToken).AsTask();
    }

    public async Task<AuditEventPage> SearchAsync(AuditEventFilter filter, CancellationToken cancellationToken = default)
    {
        IQueryable<AuditEvent> query = _dbContext.AuditEvents.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.ActorUsername))
        {
            var actorUsername = filter.ActorUsername.ToUpperInvariant();
            query = query.Where(x => x.ActorUsername != null && x.ActorUsername.ToUpper().Contains(actorUsername));
        }

        if (!string.IsNullOrWhiteSpace(filter.ActionType))
        {
            var actionType = filter.ActionType.ToUpperInvariant();
            query = query.Where(x => x.ActionType.ToUpper().Contains(actionType));
        }

        if (!string.IsNullOrWhiteSpace(filter.EntityType))
        {
            var entityType = filter.EntityType.ToUpperInvariant();
            query = query.Where(x => x.EntityType.ToUpper().Contains(entityType));
        }

        if (!string.IsNullOrWhiteSpace(filter.EntityId))
        {
            var entityId = filter.EntityId.ToUpperInvariant();
            query = query.Where(x => x.EntityId != null && x.EntityId.ToUpper().Contains(entityId));
        }

        if (!string.IsNullOrWhiteSpace(filter.Outcome))
        {
            var outcome = filter.Outcome.ToUpperInvariant();
            query = query.Where(x => x.Outcome.ToUpper().Contains(outcome));
        }

        if (!string.IsNullOrWhiteSpace(filter.CorrelationId))
        {
            var correlationId = filter.CorrelationId.ToUpperInvariant();
            query = query.Where(x => x.CorrelationId.ToUpper().Contains(correlationId));
        }

        if (filter.FromUtc.HasValue)
        {
            query = query.Where(x => x.OccurredAtUtc >= filter.FromUtc.Value);
        }

        if (filter.ToUtc.HasValue)
        {
            query = query.Where(x => x.OccurredAtUtc <= filter.ToUtc.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.OccurredAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return new AuditEventPage
        {
            Items = items,
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }
}
