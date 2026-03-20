using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.Audit;

public sealed class AuditEventPage
{
    public IReadOnlyList<AuditEvent> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}
