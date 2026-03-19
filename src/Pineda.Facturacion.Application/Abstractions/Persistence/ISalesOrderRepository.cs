using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface ISalesOrderRepository
{
    Task<SalesOrder?> GetByLegacyImportRecordIdAsync(long legacyImportRecordId, CancellationToken cancellationToken = default);

    Task AddAsync(SalesOrder salesOrder, CancellationToken cancellationToken = default);
}
