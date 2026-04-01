using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface IExternalRepBaseDocumentRepository
{
    Task<ExternalRepBaseDocument?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<ExternalRepBaseDocument?> GetByUuidAsync(string uuid, CancellationToken cancellationToken = default);

    Task AddAsync(ExternalRepBaseDocument document, CancellationToken cancellationToken = default);
}
