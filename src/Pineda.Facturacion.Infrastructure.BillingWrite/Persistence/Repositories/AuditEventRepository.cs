using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Entities;

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
}
