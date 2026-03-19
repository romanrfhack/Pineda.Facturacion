using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface ISalesOrderRepository
{
    Task AddAsync(SalesOrder salesOrder, CancellationToken cancellationToken = default);
}
