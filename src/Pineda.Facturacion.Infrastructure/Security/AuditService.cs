using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Abstractions.Security;
using Pineda.Facturacion.Application.Security;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.Security;

public sealed class AuditService : IAuditService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IAuditEventRepository _auditEventRepository;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IUnitOfWork _unitOfWork;

    public AuditService(
        IAuditEventRepository auditEventRepository,
        ICurrentUserAccessor currentUserAccessor,
        IHttpContextAccessor httpContextAccessor,
        IUnitOfWork unitOfWork)
    {
        _auditEventRepository = auditEventRepository;
        _currentUserAccessor = currentUserAccessor;
        _httpContextAccessor = httpContextAccessor;
        _unitOfWork = unitOfWork;
    }

    public async Task RecordAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        var httpContext = _httpContextAccessor.HttpContext;
        var correlationId = httpContext?.Request.Headers["X-Correlation-Id"].FirstOrDefault()
            ?? httpContext?.TraceIdentifier
            ?? Guid.NewGuid().ToString("N");

        var auditEvent = new AuditEvent
        {
            OccurredAtUtc = DateTime.UtcNow,
            ActorUserId = currentUser.UserId,
            ActorUsername = record.ActorUsernameOverride ?? currentUser.Username,
            ActionType = record.ActionType,
            EntityType = record.EntityType,
            EntityId = record.EntityId,
            Outcome = record.Outcome,
            CorrelationId = correlationId,
            RequestSummaryJson = SerializeSafely(record.RequestSummary),
            ResponseSummaryJson = SerializeSafely(record.ResponseSummary),
            ErrorMessage = record.ErrorMessage,
            IpAddress = httpContext?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = httpContext?.Request.Headers.UserAgent.ToString(),
            CreatedAtUtc = DateTime.UtcNow
        };

        await _auditEventRepository.AddAsync(auditEvent, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static string? SerializeSafely(object? value)
    {
        if (value is null)
        {
            return null;
        }

        return JsonSerializer.Serialize(value, JsonOptions);
    }
}
