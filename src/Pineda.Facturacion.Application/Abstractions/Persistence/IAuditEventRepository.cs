using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Application.UseCases.Audit;

namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface IAuditEventRepository
{
    Task AddAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);
    Task<AuditEventPage> SearchAsync(AuditEventFilter filter, CancellationToken cancellationToken = default);
}
