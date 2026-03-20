using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface IAppUserRepository
{
    Task<AppUser?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<AppUser?> GetByNormalizedUsernameAsync(string normalizedUsername, CancellationToken cancellationToken = default);

    Task<AppUser?> GetTrackedByNormalizedUsernameAsync(string normalizedUsername, CancellationToken cancellationToken = default);

    Task AddAsync(AppUser appUser, CancellationToken cancellationToken = default);
}
