namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface IAppUserRoleRepository
{
    Task<bool> ExistsAsync(long userId, long roleId, CancellationToken cancellationToken = default);

    Task AddAsync(long userId, long roleId, DateTime assignedAtUtc, CancellationToken cancellationToken = default);
}
