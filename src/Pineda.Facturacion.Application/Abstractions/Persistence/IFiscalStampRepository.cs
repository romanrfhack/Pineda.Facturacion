using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface IFiscalStampRepository
{
    Task<FiscalStamp?> GetByFiscalDocumentIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default);

    Task<FiscalStamp?> GetTrackedByFiscalDocumentIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default);

    Task<FiscalStamp?> GetByUuidAsync(string uuid, CancellationToken cancellationToken = default);

    Task<FiscalStamp?> GetTrackedByUuidAsync(string uuid, CancellationToken cancellationToken = default);

    async Task<IReadOnlyList<FiscalStamp>> GetByFiscalDocumentIdsAsync(
        IReadOnlyCollection<long> fiscalDocumentIds,
        CancellationToken cancellationToken = default)
    {
        if (fiscalDocumentIds.Count == 0)
        {
            return [];
        }

        var stamps = await Task.WhenAll(fiscalDocumentIds.Select(id => GetByFiscalDocumentIdAsync(id, cancellationToken)));
        return stamps.Where(stamp => stamp is not null).Cast<FiscalStamp>().ToList();
    }

    Task AddAsync(FiscalStamp fiscalStamp, CancellationToken cancellationToken = default);
}
