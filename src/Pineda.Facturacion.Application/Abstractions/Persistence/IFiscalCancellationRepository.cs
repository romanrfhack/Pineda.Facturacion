using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface IFiscalCancellationRepository
{
    Task<FiscalCancellation?> GetByFiscalDocumentIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default);

    Task<FiscalCancellation?> GetTrackedByFiscalDocumentIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default);

    Task<FiscalCancellation?> GetByFiscalStampIdAsync(long fiscalStampId, CancellationToken cancellationToken = default);

    Task<FiscalCancellation?> GetTrackedByFiscalStampIdAsync(long fiscalStampId, CancellationToken cancellationToken = default);

    async Task<IReadOnlyList<FiscalCancellation>> GetByFiscalDocumentIdsAsync(
        IReadOnlyCollection<long> fiscalDocumentIds,
        CancellationToken cancellationToken = default)
    {
        if (fiscalDocumentIds.Count == 0)
        {
            return [];
        }

        var cancellations = await Task.WhenAll(
            fiscalDocumentIds
                .Distinct()
                .Select(id => GetByFiscalDocumentIdAsync(id, cancellationToken)));

        return cancellations.Where(cancellation => cancellation is not null).Cast<FiscalCancellation>().ToList();
    }

    Task AddAsync(FiscalCancellation fiscalCancellation, CancellationToken cancellationToken = default);
}
