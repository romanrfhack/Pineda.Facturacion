using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface IInternalRepBaseDocumentStateRepository
{
    Task<IReadOnlyDictionary<long, InternalRepBaseDocumentState>> GetByFiscalDocumentIdsAsync(
        IReadOnlyCollection<long> fiscalDocumentIds,
        CancellationToken cancellationToken = default);

    Task<InternalRepBaseDocumentState?> GetByFiscalDocumentIdAsync(
        long fiscalDocumentId,
        CancellationToken cancellationToken = default);

    Task UpsertAsync(InternalRepBaseDocumentState state, CancellationToken cancellationToken = default);
}
