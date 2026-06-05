using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface IFiscalReceiverRepository
{
    Task<IReadOnlyList<FiscalReceiver>> SearchAsync(string query, CancellationToken cancellationToken = default, bool activeOnly = false);

    Task<FiscalReceiver?> GetByRfcAsync(string normalizedRfc, CancellationToken cancellationToken = default);

    async Task<IReadOnlyList<FiscalReceiver>> GetByRfcsAsync(
        IReadOnlyCollection<string> normalizedRfcs,
        CancellationToken cancellationToken = default)
    {
        if (normalizedRfcs.Count == 0)
        {
            return [];
        }

        var receivers = await Task.WhenAll(normalizedRfcs.Select(rfc => GetByRfcAsync(rfc, cancellationToken)));
        return receivers.Where(receiver => receiver is not null).Cast<FiscalReceiver>().ToList();
    }

    Task<FiscalReceiver?> GetByIdAsync(long fiscalReceiverId, CancellationToken cancellationToken = default);

    async Task<IReadOnlyList<FiscalReceiver>> GetByIdsAsync(
        IReadOnlyCollection<long> fiscalReceiverIds,
        CancellationToken cancellationToken = default)
    {
        if (fiscalReceiverIds.Count == 0)
        {
            return [];
        }

        var receivers = await Task.WhenAll(fiscalReceiverIds.Select(id => GetByIdAsync(id, cancellationToken)));
        return receivers.Where(receiver => receiver is not null).Cast<FiscalReceiver>().ToList();
    }

    Task<IReadOnlyList<FiscalReceiverSpecialFieldDefinition>> GetActiveSpecialFieldDefinitionsAsync(CancellationToken cancellationToken = default);

    Task AddAsync(FiscalReceiver fiscalReceiver, CancellationToken cancellationToken = default);

    Task UpdateAsync(FiscalReceiver fiscalReceiver, CancellationToken cancellationToken = default);
}
