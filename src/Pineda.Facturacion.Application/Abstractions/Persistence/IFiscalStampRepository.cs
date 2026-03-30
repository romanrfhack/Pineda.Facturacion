using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface IFiscalStampRepository
{
    Task<FiscalStamp?> GetByFiscalDocumentIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default);

    Task<FiscalStamp?> GetTrackedByFiscalDocumentIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default);

    Task<FiscalStamp?> GetByUuidAsync(string uuid, CancellationToken cancellationToken = default);

    Task<FiscalStamp?> GetTrackedByUuidAsync(string uuid, CancellationToken cancellationToken = default);

    Task AddAsync(FiscalStamp fiscalStamp, CancellationToken cancellationToken = default);
}
