using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Abstractions.Security;
using Pineda.Facturacion.Application.Security;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Infrastructure.Options;
using Pineda.Facturacion.Infrastructure.Security;

namespace Pineda.Facturacion.UnitTests;

public class IdentityBootstrapServiceTests
{
    [Fact]
    public async Task ExecuteAsync_SeedsRoles_AndDefaultUsers_InNonProduction()
    {
        var roleRepository = new BootstrapFakeAppRoleRepository();
        var userRepository = new BootstrapFakeAppUserRepository();
        var userRoleRepository = new BootstrapFakeAppUserRoleRepository();
        var passwordHasher = new BootstrapFakePasswordHasher();
        var unitOfWork = new BootstrapFakeUnitOfWork();
        var service = CreateService(
            roleRepository,
            userRepository,
            userRoleRepository,
            passwordHasher,
            unitOfWork,
            environmentName: "Sandbox",
            bootstrapAdminOptions: new BootstrapAdminOptions
            {
                Enabled = false
            },
            bootstrapSeedOptions: new BootstrapSeedOptions
            {
                SeedDefaultRoles = true,
                SeedDefaultTestUsers = true,
                DefaultTestUserPassword = "SandboxOnly123!"
            });

        await service.ExecuteAsync();

        Assert.Equal(4, roleRepository.Stored.Count);
        Assert.Contains(userRepository.Stored.Values, user => user.Username == "admin.test");
        Assert.Contains(userRepository.Stored.Values, user => user.Username == "supervisor.test");
        Assert.Contains(userRepository.Stored.Values, user => user.Username == "operator.test");
        Assert.Contains(userRepository.Stored.Values, user => user.Username == "auditor.test");
        Assert.All(userRepository.Stored.Values, user => Assert.True(user.IsActive));
        Assert.All(userRepository.Stored.Values, user => Assert.StartsWith("HASH::", user.PasswordHash, StringComparison.Ordinal));
        Assert.Equal(4, userRoleRepository.Assignments.Count);
        Assert.Contains(userRoleRepository.Assignments, assignment => assignment.RoleName == AppRoleNames.Admin && assignment.Username == "admin.test");
        Assert.True(unitOfWork.SaveChangesCallCount >= 1);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotSeedDefaultUsers_InProduction()
    {
        var roleRepository = new BootstrapFakeAppRoleRepository();
        var userRepository = new BootstrapFakeAppUserRepository();
        var service = CreateService(
            roleRepository,
            userRepository,
            new BootstrapFakeAppUserRoleRepository(),
            new BootstrapFakePasswordHasher(),
            new BootstrapFakeUnitOfWork(),
            environmentName: "Production",
            bootstrapAdminOptions: new BootstrapAdminOptions
            {
                Enabled = true,
                Username = "admin",
                DisplayName = "Admin",
                Password = "Ignored123!"
            },
            bootstrapSeedOptions: new BootstrapSeedOptions
            {
                SeedDefaultRoles = true,
                SeedDefaultTestUsers = true,
                DefaultTestUserPassword = "SandboxOnly123!"
            });

        await service.ExecuteAsync();

        Assert.Equal(4, roleRepository.Stored.Count);
        Assert.Empty(userRepository.Stored);
    }

    [Fact]
    public async Task ExecuteAsync_AssignsConfiguredBootstrapAdmin_InNonProduction()
    {
        var roleRepository = new BootstrapFakeAppRoleRepository();
        var userRepository = new BootstrapFakeAppUserRepository();
        var userRoleRepository = new BootstrapFakeAppUserRoleRepository();
        var passwordHasher = new BootstrapFakePasswordHasher();
        var service = CreateService(
            roleRepository,
            userRepository,
            userRoleRepository,
            passwordHasher,
            new BootstrapFakeUnitOfWork(),
            environmentName: "Local",
            bootstrapAdminOptions: new BootstrapAdminOptions
            {
                Enabled = true,
                Username = "bootstrap.admin",
                DisplayName = "Bootstrap Admin",
                Password = "LocalOnly123!"
            },
            bootstrapSeedOptions: new BootstrapSeedOptions
            {
                SeedDefaultRoles = true,
                SeedDefaultTestUsers = false
            });

        await service.ExecuteAsync();

        var user = Assert.Single(userRepository.Stored.Values);
        Assert.Equal("bootstrap.admin", user.Username);
        Assert.Equal("HASH::bootstrap.admin::LocalOnly123!", user.PasswordHash);
        Assert.Contains(userRoleRepository.Assignments, assignment => assignment.Username == "bootstrap.admin" && assignment.RoleName == AppRoleNames.Admin);
        Assert.Single(passwordHasher.HashCalls);
    }

    [Fact]
    public async Task ExecuteAsync_RehashesSeededTestUsers_WhenConfigured()
    {
        var existingUser = new AppUser
        {
            Id = 10,
            Username = "operator.test",
            NormalizedUsername = "OPERATOR.TEST",
            DisplayName = "Old Operator",
            PasswordHash = "OLD_HASH",
            IsActive = false,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-5),
            UpdatedAtUtc = DateTime.UtcNow.AddDays(-5)
        };
        var roleRepository = new BootstrapFakeAppRoleRepository();
        var userRepository = new BootstrapFakeAppUserRepository(existingUser);
        var userRoleRepository = new BootstrapFakeAppUserRoleRepository();
        var passwordHasher = new BootstrapFakePasswordHasher();
        var service = CreateService(
            roleRepository,
            userRepository,
            userRoleRepository,
            passwordHasher,
            new BootstrapFakeUnitOfWork(),
            environmentName: "Testing",
            bootstrapAdminOptions: new BootstrapAdminOptions(),
            bootstrapSeedOptions: new BootstrapSeedOptions
            {
                SeedDefaultRoles = true,
                SeedDefaultTestUsers = true,
                DefaultTestUserPassword = "Reset123!"
            });

        await service.ExecuteAsync();

        var updated = userRepository.Stored["OPERATOR.TEST"];
        Assert.True(updated.IsActive);
        Assert.Equal("Sandbox Operator", updated.DisplayName);
        Assert.Equal("HASH::operator.test::Reset123!", updated.PasswordHash);
        Assert.Contains(userRoleRepository.Assignments, assignment => assignment.Username == "operator.test" && assignment.RoleName == AppRoleNames.FiscalOperator);
    }

    private static IdentityBootstrapService CreateService(
        BootstrapFakeAppRoleRepository roleRepository,
        BootstrapFakeAppUserRepository userRepository,
        BootstrapFakeAppUserRoleRepository userRoleRepository,
        BootstrapFakePasswordHasher passwordHasher,
        BootstrapFakeUnitOfWork unitOfWork,
        string environmentName,
        BootstrapAdminOptions bootstrapAdminOptions,
        BootstrapSeedOptions bootstrapSeedOptions)
    {
        userRoleRepository.Attach(userRepository, roleRepository);

        return new IdentityBootstrapService(
            roleRepository,
            userRepository,
            userRoleRepository,
            passwordHasher,
            unitOfWork,
            new BootstrapFakeHostEnvironment { EnvironmentName = environmentName },
            Options.Create(bootstrapAdminOptions),
            Options.Create(bootstrapSeedOptions),
            NullLogger<IdentityBootstrapService>.Instance);
    }
}

internal sealed class BootstrapFakeAppRoleRepository : IAppRoleRepository
{
    public Dictionary<string, AppRole> Stored { get; } = [];
    private long _nextId = 1;

    public Task<AppRole?> GetByNormalizedNameAsync(string normalizedName, CancellationToken cancellationToken = default)
    {
        Stored.TryGetValue(normalizedName, out var role);
        return Task.FromResult(role);
    }

    public Task<IReadOnlyList<AppRole>> GetByNormalizedNamesAsync(IEnumerable<string> normalizedNames, CancellationToken cancellationToken = default)
    {
        var roles = normalizedNames
            .Select(name => Stored.TryGetValue(name, out var role) ? role : null)
            .Where(role => role is not null)
            .Cast<AppRole>()
            .ToArray();
        return Task.FromResult<IReadOnlyList<AppRole>>(roles);
    }

    public Task AddAsync(AppRole appRole, CancellationToken cancellationToken = default)
    {
        appRole.Id = _nextId++;
        Stored[appRole.NormalizedName] = appRole;
        return Task.CompletedTask;
    }
}

internal sealed class BootstrapFakeAppUserRepository : IAppUserRepository
{
    public Dictionary<string, AppUser> Stored { get; } = [];
    private long _nextId = 1;

    public BootstrapFakeAppUserRepository(params AppUser[] initialUsers)
    {
        foreach (var user in initialUsers)
        {
            Stored[user.NormalizedUsername] = user;
            _nextId = Math.Max(_nextId, user.Id + 1);
        }
    }

    public Task<AppUser?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Stored.Values.SingleOrDefault(user => user.Id == id));
    }

    public Task<AppUser?> GetByNormalizedUsernameAsync(string normalizedUsername, CancellationToken cancellationToken = default)
    {
        Stored.TryGetValue(normalizedUsername, out var user);
        return Task.FromResult(user);
    }

    public Task<AppUser?> GetTrackedByNormalizedUsernameAsync(string normalizedUsername, CancellationToken cancellationToken = default)
    {
        Stored.TryGetValue(normalizedUsername, out var user);
        return Task.FromResult(user);
    }

    public Task AddAsync(AppUser appUser, CancellationToken cancellationToken = default)
    {
        appUser.Id = _nextId++;
        Stored[appUser.NormalizedUsername] = appUser;
        return Task.CompletedTask;
    }
}

internal sealed class BootstrapFakeAppUserRoleRepository : IAppUserRoleRepository
{
    public List<(long UserId, long RoleId, string Username, string RoleName)> Assignments { get; } = [];
    private BootstrapFakeAppUserRepository? _userRepository;
    private BootstrapFakeAppRoleRepository? _roleRepository;

    public void Attach(BootstrapFakeAppUserRepository userRepository, BootstrapFakeAppRoleRepository roleRepository)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
    }

    public Task<bool> ExistsAsync(long userId, long roleId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Assignments.Any(assignment => assignment.UserId == userId && assignment.RoleId == roleId));
    }

    public Task AddAsync(long userId, long roleId, DateTime assignedAtUtc, CancellationToken cancellationToken = default)
    {
        var username = _userRepository?.Stored.Values.Single(user => user.Id == userId).Username ?? userId.ToString();
        var roleName = _roleRepository?.Stored.Values.Single(role => role.Id == roleId).Name ?? roleId.ToString();
        Assignments.Add((userId, roleId, username, roleName));
        return Task.CompletedTask;
    }
}

internal sealed class BootstrapFakePasswordHasher : IPasswordHasher
{
    public List<(string Username, string Password)> HashCalls { get; } = [];

    public string HashPassword(AppUser user, string password)
    {
        HashCalls.Add((user.Username, password));
        return $"HASH::{user.Username}::{password}";
    }

    public bool VerifyPassword(AppUser user, string password)
    {
        return string.Equals(user.PasswordHash, $"HASH::{user.Username}::{password}", StringComparison.Ordinal);
    }
}

internal sealed class BootstrapFakeUnitOfWork : IUnitOfWork
{
    public int SaveChangesCallCount { get; private set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}

internal sealed class BootstrapFakeHostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = Environments.Development;

    public string ApplicationName { get; set; } = "Pineda.Facturacion.Tests";

    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
