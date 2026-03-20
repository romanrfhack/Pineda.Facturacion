using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface IFiscalReceiverRepository
{
    Task<IReadOnlyList<FiscalReceiver>> SearchAsync(string query, CancellationToken cancellationToken = default);

    Task<FiscalReceiver?> GetByRfcAsync(string normalizedRfc, CancellationToken cancellationToken = default);

    Task<FiscalReceiver?> GetByIdAsync(long fiscalReceiverId, CancellationToken cancellationToken = default);

    Task AddAsync(FiscalReceiver fiscalReceiver, CancellationToken cancellationToken = default);

    Task UpdateAsync(FiscalReceiver fiscalReceiver, CancellationToken cancellationToken = default);
}
