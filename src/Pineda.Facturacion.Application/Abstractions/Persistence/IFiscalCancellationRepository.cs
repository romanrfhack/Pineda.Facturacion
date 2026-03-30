using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface IFiscalCancellationRepository
{
    Task<FiscalCancellation?> GetByFiscalDocumentIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default);

    Task<FiscalCancellation?> GetTrackedByFiscalDocumentIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default);

    Task<FiscalCancellation?> GetByFiscalStampIdAsync(long fiscalStampId, CancellationToken cancellationToken = default);

    Task<FiscalCancellation?> GetTrackedByFiscalStampIdAsync(long fiscalStampId, CancellationToken cancellationToken = default);

    Task AddAsync(FiscalCancellation fiscalCancellation, CancellationToken cancellationToken = default);
}
