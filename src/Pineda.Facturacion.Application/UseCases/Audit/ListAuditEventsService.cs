using Pineda.Facturacion.Application.Abstractions.Persistence;

namespace Pineda.Facturacion.Application.UseCases.Audit;

public sealed class ListAuditEventsService
{
    private readonly IAuditEventRepository _auditEventRepository;

    public ListAuditEventsService(IAuditEventRepository auditEventRepository)
    {
        _auditEventRepository = auditEventRepository;
    }

    public Task<AuditEventPage> ExecuteAsync(AuditEventFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var normalizedFilter = new AuditEventFilter
        {
            Page = filter.Page < 1 ? 1 : filter.Page,
            PageSize = filter.PageSize switch
            {
                < 1 => 25,
                > 100 => 100,
                _ => filter.PageSize
            },
            ActorUsername = filter.ActorUsername?.Trim(),
            ActionType = filter.ActionType?.Trim(),
            EntityType = filter.EntityType?.Trim(),
            EntityId = filter.EntityId?.Trim(),
            Outcome = filter.Outcome?.Trim(),
            FromUtc = filter.FromUtc,
            ToUtc = filter.ToUtc,
            CorrelationId = filter.CorrelationId?.Trim()
        };

        return _auditEventRepository.SearchAsync(normalizedFilter, cancellationToken);
    }
}
