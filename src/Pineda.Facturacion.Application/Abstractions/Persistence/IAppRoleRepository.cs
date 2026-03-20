using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface IAppRoleRepository
{
    Task<AppRole?> GetByNormalizedNameAsync(string normalizedName, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AppRole>> GetByNormalizedNamesAsync(IEnumerable<string> normalizedNames, CancellationToken cancellationToken = default);

    Task AddAsync(AppRole appRole, CancellationToken cancellationToken = default);
}
