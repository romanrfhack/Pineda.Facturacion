using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Abstractions.Security;
using Pineda.Facturacion.Application.Security;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Infrastructure.Options;

namespace Pineda.Facturacion.Infrastructure.Security;

public sealed class DevIdentitySeedService
{
    private static readonly Dictionary<string, string> KnownRolesByNormalizedName =
        AppRoleNames.All.ToDictionary(role => role.ToUpperInvariant(), StringComparer.Ordinal);

    private readonly IAppRoleRepository _roleRepository;
    private readonly IAppUserRepository _userRepository;
    private readonly IAppUserRoleRepository _userRoleRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IOptions<DevIdentitySeedOptions> _options;
    private readonly ILogger<DevIdentitySeedService> _logger;

    public DevIdentitySeedService(
        IAppRoleRepository roleRepository,
        IAppUserRepository userRepository,
        IAppUserRoleRepository userRoleRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork,
        IHostEnvironment hostEnvironment,
        IOptions<DevIdentitySeedOptions> options,
        ILogger<DevIdentitySeedService> logger)
    {
        _roleRepository = roleRepository;
        _userRepository = userRepository;
        _userRoleRepository = userRoleRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
        _hostEnvironment = hostEnvironment;
        _options = options;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var options = _options.Value;
        if (!options.Enabled)
        {
            return;
        }

        if (!IsAllowedDevSeedEnvironment())
        {
            _logger.LogError(
                "Dev identity seed is enabled in disallowed environment {EnvironmentName}.",
                _hostEnvironment.EnvironmentName);
            throw new InvalidOperationException(
                $"Dev identity seed is not allowed in environment '{_hostEnvironment.EnvironmentName}'.");
        }

        if (string.IsNullOrWhiteSpace(options.DefaultPassword))
        {
            _logger.LogError("Dev identity seed is enabled but Auth:DevUsers:DefaultPassword is missing.");
            throw new InvalidOperationException(
                "Configuration 'Auth:DevUsers:DefaultPassword' is required when 'Auth:DevUsers:Enabled' is true.");
        }

        var users = NormalizeUsers(options.Users ?? []);
        if (users.Count == 0)
        {
            _logger.LogWarning("Dev identity seed is enabled but no Auth:DevUsers:Users entries were configured.");
            return;
        }

        var now = DateTime.UtcNow;
        var rolesByNormalizedName = await EnsureRolesAsync(users, now, cancellationToken);

        foreach (var user in users)
        {
            await EnsureUserAsync(
                user,
                options.DefaultPassword,
                options.ResetPasswordOnStartup,
                rolesByNormalizedName,
                now,
                cancellationToken);
        }
    }

    private async Task<Dictionary<string, AppRole>> EnsureRolesAsync(
        IReadOnlyCollection<NormalizedDevUserDefinition> users,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var rolesByNormalizedName = new Dictionary<string, AppRole>(StringComparer.Ordinal);
        var changed = false;
        var normalizedRoleNames = users
            .SelectMany(user => user.NormalizedRoleNames)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        foreach (var normalizedRoleName in normalizedRoleNames)
        {
            if (!KnownRolesByNormalizedName.ContainsKey(normalizedRoleName))
            {
                throw new InvalidOperationException($"Role '{normalizedRoleName}' is not a supported application role.");
            }
        }

        foreach (var normalizedRoleName in normalizedRoleNames)
        {
            var canonicalRoleName = KnownRolesByNormalizedName[normalizedRoleName];
            var role = await _roleRepository.GetByNormalizedNameAsync(normalizedRoleName, cancellationToken);
            if (role is null)
            {
                role = new AppRole
                {
                    Name = canonicalRoleName,
                    NormalizedName = normalizedRoleName,
                    CreatedAtUtc = now
                };
                await _roleRepository.AddAsync(role, cancellationToken);
                changed = true;
            }

            rolesByNormalizedName[normalizedRoleName] = role;
        }

        if (changed)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return rolesByNormalizedName;
    }

    private async Task EnsureUserAsync(
        NormalizedDevUserDefinition definition,
        string defaultPassword,
        bool resetPasswordOnStartup,
        IReadOnlyDictionary<string, AppRole> rolesByNormalizedName,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetTrackedByNormalizedUsernameAsync(definition.NormalizedUsername, cancellationToken);
        var userChanged = false;

        if (user is null)
        {
            user = new AppUser
            {
                Username = definition.Username,
                NormalizedUsername = definition.NormalizedUsername,
                DisplayName = definition.DisplayName,
                IsActive = true,
                FailedLoginAttemptCount = 0,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            user.PasswordHash = _passwordHasher.HashPassword(user, defaultPassword);
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

            if (!string.Equals(user.DisplayName, definition.DisplayName, StringComparison.Ordinal))
            {
                user.DisplayName = definition.DisplayName;
                userChanged = true;
            }

            if (resetPasswordOnStartup)
            {
                user.PasswordHash = _passwordHasher.HashPassword(user, defaultPassword);
                userChanged = true;
            }

            if (userChanged)
            {
                user.UpdatedAtUtc = now;
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        foreach (var normalizedRoleName in definition.NormalizedRoleNames)
        {
            var role = rolesByNormalizedName[normalizedRoleName];
            if (!await _userRoleRepository.ExistsAsync(user.Id, role.Id, cancellationToken))
            {
                await _userRoleRepository.AddAsync(user.Id, role.Id, now, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        if (userChanged)
        {
            _logger.LogInformation("Bootstrapped dev identity user {Username}.", definition.Username);
        }
    }

    private static IReadOnlyList<NormalizedDevUserDefinition> NormalizeUsers(IReadOnlyCollection<DevIdentitySeedUserOptions> users)
    {
        var normalizedUsers = new List<NormalizedDevUserDefinition>();
        var seenUsernames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var user in users)
        {
            var username = user.Username.Trim();
            if (string.IsNullOrWhiteSpace(username))
            {
                throw new InvalidOperationException("Auth:DevUsers:Users contains a user with an empty username.");
            }

            var normalizedUsername = username.ToUpperInvariant();
            if (!seenUsernames.Add(normalizedUsername))
            {
                throw new InvalidOperationException($"Auth:DevUsers:Users contains duplicate username '{username}'.");
            }

            var displayName = string.IsNullOrWhiteSpace(user.DisplayName) ? username : user.DisplayName.Trim();
            var normalizedRoleNames = (user.Roles ?? [])
                .Select(role => role.Trim())
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Select(role => role.ToUpperInvariant())
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (normalizedRoleNames.Length == 0)
            {
                throw new InvalidOperationException($"Dev identity user '{username}' must have at least one role configured.");
            }

            normalizedUsers.Add(new NormalizedDevUserDefinition(
                username,
                normalizedUsername,
                displayName,
                normalizedRoleNames));
        }

        return normalizedUsers;
    }

    private bool IsAllowedDevSeedEnvironment()
    {
        return _hostEnvironment.IsDevelopment()
            || _hostEnvironment.IsEnvironment("Local")
            || _hostEnvironment.IsEnvironment("Testing")
            || _hostEnvironment.IsEnvironment("Sandbox");
    }

    private sealed record NormalizedDevUserDefinition(
        string Username,
        string NormalizedUsername,
        string DisplayName,
        IReadOnlyList<string> NormalizedRoleNames);
}
