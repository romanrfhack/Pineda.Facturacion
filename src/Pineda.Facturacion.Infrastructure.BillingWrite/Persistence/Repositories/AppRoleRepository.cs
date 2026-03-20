using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

public sealed class AppRoleRepository : IAppRoleRepository
{
    private readonly BillingDbContext _dbContext;

    public AppRoleRepository(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<AppRole?> GetByNormalizedNameAsync(string normalizedName, CancellationToken cancellationToken = default)
    {
        return _dbContext.AppRoles.AsNoTracking().SingleOrDefaultAsync(x => x.NormalizedName == normalizedName, cancellationToken);
    }

    public async Task<IReadOnlyList<AppRole>> GetByNormalizedNamesAsync(IEnumerable<string> normalizedNames, CancellationToken cancellationToken = default)
    {
        var names = normalizedNames.Distinct(StringComparer.Ordinal).ToList();
        return await _dbContext.AppRoles
            .Where(x => names.Contains(x.NormalizedName))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public Task AddAsync(AppRole appRole, CancellationToken cancellationToken = default)
    {
        return _dbContext.AppRoles.AddAsync(appRole, cancellationToken).AsTask();
    }
}
