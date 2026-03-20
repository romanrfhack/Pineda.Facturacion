using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

public sealed class AppUserRoleRepository : IAppUserRoleRepository
{
    private readonly BillingDbContext _dbContext;

    public AppUserRoleRepository(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> ExistsAsync(long userId, long roleId, CancellationToken cancellationToken = default)
    {
        return _dbContext.AppUserRoles.AnyAsync(x => x.UserId == userId && x.RoleId == roleId, cancellationToken);
    }

    public Task AddAsync(long userId, long roleId, DateTime assignedAtUtc, CancellationToken cancellationToken = default)
    {
        return _dbContext.AppUserRoles.AddAsync(new AppUserRole
        {
            UserId = userId,
            RoleId = roleId,
            AssignedAtUtc = assignedAtUtc
        }, cancellationToken).AsTask();
    }
}
