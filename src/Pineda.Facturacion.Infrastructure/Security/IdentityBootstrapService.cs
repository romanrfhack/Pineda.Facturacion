using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Abstractions.Security;
using Pineda.Facturacion.Application.Security;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Infrastructure.Options;

namespace Pineda.Facturacion.Infrastructure.Security;

public sealed class IdentityBootstrapService
{
    private static readonly SeededUserDefinition[] DefaultUsers =
    [
        new("admin.test", "Sandbox Admin", AppRoleNames.Admin),
        new("supervisor.test", "Sandbox Supervisor", AppRoleNames.FiscalSupervisor),
        new("operator.test", "Sandbox Operator", AppRoleNames.FiscalOperator),
        new("auditor.test", "Sandbox Auditor", AppRoleNames.Auditor)
    ];

    private readonly IAppRoleRepository _roleRepository;
    private readonly IAppUserRepository _userRepository;
    private readonly IAppUserRoleRepository _userRoleRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IOptions<BootstrapAdminOptions> _bootstrapAdminOptions;
    private readonly IOptions<BootstrapSeedOptions> _bootstrapSeedOptions;
    private readonly ILogger<IdentityBootstrapService> _logger;

    public IdentityBootstrapService(
        IAppRoleRepository roleRepository,
        IAppUserRepository userRepository,
        IAppUserRoleRepository userRoleRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork,
        IHostEnvironment hostEnvironment,
        IOptions<BootstrapAdminOptions> bootstrapAdminOptions,
        IOptions<BootstrapSeedOptions> bootstrapSeedOptions,
        ILogger<IdentityBootstrapService> logger)
    {
        _roleRepository = roleRepository;
        _userRepository = userRepository;
        _userRoleRepository = userRoleRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
        _hostEnvironment = hostEnvironment;
        _bootstrapAdminOptions = bootstrapAdminOptions;
        _bootstrapSeedOptions = bootstrapSeedOptions;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        if (_bootstrapSeedOptions.Value.SeedDefaultRoles)
        {
            await EnsureRolesAsync(now, cancellationToken);
        }

        if (!IsNonProductionIdentityBootstrapEnvironment())
        {
            _logger.LogInformation("Identity bootstrap skipped for environment {EnvironmentName}.", _hostEnvironment.EnvironmentName);
            return;
        }

        await EnsureBootstrapAdminAsync(now, cancellationToken);
        await EnsureDefaultTestUsersAsync(now, cancellationToken);
    }

    private async Task EnsureRolesAsync(DateTime now, CancellationToken cancellationToken)
    {
        var changed = false;
        foreach (var roleName in AppRoleNames.All)
        {
            var normalizedName = roleName.ToUpperInvariant();
            var existing = await _roleRepository.GetByNormalizedNameAsync(normalizedName, cancellationToken);
            if (existing is not null)
            {
                continue;
            }

            await _roleRepository.AddAsync(new AppRole
            {
                Name = roleName,
                NormalizedName = normalizedName,
                CreatedAtUtc = now
            }, cancellationToken);
            changed = true;
        }

        if (changed)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task EnsureBootstrapAdminAsync(DateTime now, CancellationToken cancellationToken)
    {
        var options = _bootstrapAdminOptions.Value;
        if (!options.Enabled || string.IsNullOrWhiteSpace(options.Username) || string.IsNullOrWhiteSpace(options.Password))
        {
            return;
        }

        await EnsureUserWithRoleAsync(
            options.Username.Trim(),
            string.IsNullOrWhiteSpace(options.DisplayName) ? options.Username.Trim() : options.DisplayName.Trim(),
            options.Password,
            AppRoleNames.Admin,
            now,
            overwritePassword: false,
            cancellationToken);
    }

    private async Task EnsureDefaultTestUsersAsync(DateTime now, CancellationToken cancellationToken)
    {
        var options = _bootstrapSeedOptions.Value;
        if (!options.SeedDefaultTestUsers || string.IsNullOrWhiteSpace(options.DefaultTestUserPassword))
        {
            return;
        }

        foreach (var definition in DefaultUsers)
        {
            await EnsureUserWithRoleAsync(
                definition.Username,
                definition.DisplayName,
                options.DefaultTestUserPassword,
                definition.RoleName,
                now,
                overwritePassword: true,
                cancellationToken);
        }
    }

    private async Task EnsureUserWithRoleAsync(
        string username,
        string displayName,
        string password,
        string roleName,
        DateTime now,
        bool overwritePassword,
        CancellationToken cancellationToken)
    {
        var normalizedUsername = username.ToUpperInvariant();
        var user = await _userRepository.GetTrackedByNormalizedUsernameAsync(normalizedUsername, cancellationToken);
        var userChanged = false;

        if (user is null)
        {
            user = new AppUser
            {
                Username = username,
                NormalizedUsername = normalizedUsername,
                DisplayName = displayName,
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            user.PasswordHash = _passwordHasher.HashPassword(user, password);
            await _userRepository.AddAsync(user, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            userChanged = true;
        }
        else
        {
            if (!user.IsActive)
            {
                user.IsActive = true;
                userChanged = true;
            }

            if (!string.Equals(user.DisplayName, displayName, StringComparison.Ordinal))
            {
                user.DisplayName = displayName;
                userChanged = true;
            }

            if (overwritePassword)
            {
                user.PasswordHash = _passwordHasher.HashPassword(user, password);
                userChanged = true;
            }

            if (userChanged)
            {
                user.UpdatedAtUtc = now;
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        var role = await _roleRepository.GetByNormalizedNameAsync(roleName.ToUpperInvariant(), cancellationToken);
        if (role is null)
        {
            throw new InvalidOperationException($"Role '{roleName}' was not available during identity bootstrap.");
        }

        if (!await _userRoleRepository.ExistsAsync(user.Id, role.Id, cancellationToken))
        {
            await _userRoleRepository.AddAsync(user.Id, role.Id, now, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        if (userChanged)
        {
            _logger.LogInformation("Bootstrapped identity user {Username} with role {RoleName}.", username, roleName);
        }
    }

    private bool IsNonProductionIdentityBootstrapEnvironment()
    {
        return _hostEnvironment.IsDevelopment()
            || _hostEnvironment.IsEnvironment("Local")
            || _hostEnvironment.IsEnvironment("Testing")
            || _hostEnvironment.IsEnvironment("Sandbox");
    }

    private sealed record SeededUserDefinition(string Username, string DisplayName, string RoleName);
}
