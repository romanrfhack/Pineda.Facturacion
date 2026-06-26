using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface IFiscalReceiverRepository
{
    Task<IReadOnlyList<FiscalReceiver>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        return SearchAsync(query, cancellationToken, false);
    }

    async Task<IReadOnlyList<FiscalReceiver>> SearchAsync(string query, CancellationToken cancellationToken, bool activeOnly)
    {
        var receivers = await SearchAsync(query, cancellationToken);
        if (!activeOnly || receivers.Count == 0)
        {
            return receivers;
        }

        var activeReceivers = new List<FiscalReceiver>(receivers.Count);
        foreach (var receiver in receivers)
        {
            if (receiver.IsActive)
            {
                activeReceivers.Add(receiver);
            }
        }

        return activeReceivers;
    }

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
