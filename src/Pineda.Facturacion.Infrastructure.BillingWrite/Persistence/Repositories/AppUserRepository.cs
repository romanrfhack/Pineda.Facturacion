using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

public sealed class AppUserRepository : IAppUserRepository
{
    private readonly BillingDbContext _dbContext;

    public AppUserRepository(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<AppUser?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return _dbContext.AppUsers
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<AppUser?> GetByNormalizedUsernameAsync(string normalizedUsername, CancellationToken cancellationToken = default)
    {
        return _dbContext.AppUsers
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.NormalizedUsername == normalizedUsername, cancellationToken);
    }

    public Task<AppUser?> GetTrackedByNormalizedUsernameAsync(string normalizedUsername, CancellationToken cancellationToken = default)
    {
        return _dbContext.AppUsers
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(x => x.NormalizedUsername == normalizedUsername, cancellationToken);
    }

    public Task AddAsync(AppUser appUser, CancellationToken cancellationToken = default)
    {
        return _dbContext.AppUsers.AddAsync(appUser, cancellationToken).AsTask();
    }
}
