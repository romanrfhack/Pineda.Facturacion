using Pineda.Facturacion.Application.Security;

namespace Pineda.Facturacion.Application.Abstractions.Security;

public interface IAuditService
{
    Task RecordAsync(AuditRecord record, CancellationToken cancellationToken = default);
}
