using Pineda.Facturacion.Application.Abstractions.Security;
using Pineda.Facturacion.Application.Security;

namespace Pineda.Facturacion.Api.Security;

public static class AuditApiHelper
{
    public static Task RecordAsync(
        IAuditService auditService,
        string actionType,
        string entityType,
        string? entityId,
        string outcome,
        object? requestSummary,
        object? responseSummary,
        string? errorMessage,
        CancellationToken cancellationToken,
        string? actorUsernameOverride = null)
    {
        return auditService.RecordAsync(new AuditRecord
        {
            ActionType = actionType,
            EntityType = entityType,
            EntityId = entityId,
            Outcome = outcome,
            RequestSummary = requestSummary,
            ResponseSummary = responseSummary,
            ErrorMessage = errorMessage,
            ActorUsernameOverride = actorUsernameOverride
        }, cancellationToken);
    }
}
