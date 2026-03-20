using Microsoft.AspNetCore.Http.HttpResults;
using Pineda.Facturacion.Application.Security;
using Pineda.Facturacion.Application.UseCases.Audit;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Api.Endpoints;

public static class AuditEventsEndpoints
{
    public static IEndpointRouteBuilder MapAuditEventsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/api/audit-events", ListAuditEventsAsync)
            .WithTags("Audit")
            .RequireAuthorization(AuthorizationPolicyNames.AuditRead)
            .WithName("ListAuditEvents")
            .WithSummary("List audit events with safe filters and paging")
            .Produces<AuditEventListResponse>(StatusCodes.Status200OK);

        return endpoints;
    }

    private static async Task<Ok<AuditEventListResponse>> ListAuditEventsAsync(
        int? page,
        int? pageSize,
        string? actorUsername,
        string? actionType,
        string? entityType,
        string? entityId,
        string? outcome,
        DateTime? fromUtc,
        DateTime? toUtc,
        string? correlationId,
        ListAuditEventsService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(new AuditEventFilter
        {
            Page = page ?? 1,
            PageSize = pageSize ?? 25,
            ActorUsername = actorUsername,
            ActionType = actionType,
            EntityType = entityType,
            EntityId = entityId,
            Outcome = outcome,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            CorrelationId = correlationId
        }, cancellationToken);

        return TypedResults.Ok(new AuditEventListResponse
        {
            Page = result.Page,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount,
            Items = result.Items.Select(MapEvent).ToList()
        });
    }

    private static AuditEventResponse MapEvent(AuditEvent auditEvent)
    {
        return new AuditEventResponse
        {
            Id = auditEvent.Id,
            OccurredAtUtc = auditEvent.OccurredAtUtc,
            ActorUserId = auditEvent.ActorUserId,
            ActorUsername = auditEvent.ActorUsername,
            ActionType = auditEvent.ActionType,
            EntityType = auditEvent.EntityType,
            EntityId = auditEvent.EntityId,
            Outcome = auditEvent.Outcome,
            CorrelationId = auditEvent.CorrelationId,
            RequestSummaryJson = auditEvent.RequestSummaryJson,
            ResponseSummaryJson = auditEvent.ResponseSummaryJson,
            ErrorMessage = auditEvent.ErrorMessage,
            IpAddress = auditEvent.IpAddress,
            UserAgent = auditEvent.UserAgent,
            CreatedAtUtc = auditEvent.CreatedAtUtc
        };
    }

    public sealed class AuditEventListResponse
    {
        public int Page { get; init; }
        public int PageSize { get; init; }
        public int TotalCount { get; init; }
        public IReadOnlyList<AuditEventResponse> Items { get; init; } = [];
    }

    public sealed class AuditEventResponse
    {
        public long Id { get; init; }
        public DateTime OccurredAtUtc { get; init; }
        public long? ActorUserId { get; init; }
        public string? ActorUsername { get; init; }
        public string ActionType { get; init; } = string.Empty;
        public string EntityType { get; init; } = string.Empty;
        public string? EntityId { get; init; }
        public string Outcome { get; init; } = string.Empty;
        public string CorrelationId { get; init; } = string.Empty;
        public string? RequestSummaryJson { get; init; }
        public string? ResponseSummaryJson { get; init; }
        public string? ErrorMessage { get; init; }
        public string? IpAddress { get; init; }
        public string? UserAgent { get; init; }
        public DateTime CreatedAtUtc { get; init; }
    }
}
