using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface IIssuerProfileRepository
{
    Task<IssuerProfile?> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<IssuerProfile?> GetByIdAsync(long issuerProfileId, CancellationToken cancellationToken = default);

    Task AddAsync(IssuerProfile issuerProfile, CancellationToken cancellationToken = default);

    Task UpdateAsync(IssuerProfile issuerProfile, CancellationToken cancellationToken = default);
}
