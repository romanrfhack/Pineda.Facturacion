using Microsoft.Extensions.Logging;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Abstractions.Security;
using Pineda.Facturacion.Application.Security;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.Security;

public sealed class InitialProductionIdentitySeedService
{
    private static readonly SeededUserDefinition[] InitialUsers =
    [
        new("martha.pineda", "Martha Pineda", "Mp630726", AppRoleNames.Admin),
        new("memo.aguirre", "Memo Aguirre", "Ga560201", AppRoleNames.Admin),
        new("roman.romero", "Roman Romero", "hackGURU$1", AppRoleNames.Admin)
    ];

    private readonly IAppRoleRepository _roleRepository;
    private readonly IAppUserRepository _userRepository;
    private readonly IAppUserRoleRepository _userRoleRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<InitialProductionIdentitySeedService> _logger;

    public InitialProductionIdentitySeedService(
        IAppRoleRepository roleRepository,
        IAppUserRepository userRepository,
        IAppUserRoleRepository userRoleRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork,
        ILogger<InitialProductionIdentitySeedService> logger)
    {
        _roleRepository = roleRepository;
        _userRepository = userRepository;
        _userRoleRepository = userRoleRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<InitialProductionIdentitySeedResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var result = new InitialProductionIdentitySeedResult();

        var adminRole = await EnsureRoleAsync(
            AppRoleNames.Admin,
            "Administración total del sistema",
            now,
            result,
            cancellationToken);

        foreach (var definition in InitialUsers)
        {
            var normalizedUsername = definition.Username.ToUpperInvariant();
            var existingUser = await _userRepository.GetTrackedByNormalizedUsernameAsync(normalizedUsername, cancellationToken);

            if (existingUser is null)
            {
                existingUser = new AppUser
                {
                    Username = definition.Username,
                    NormalizedUsername = normalizedUsername,
                    DisplayName = definition.DisplayName,
                    IsActive = true,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                };
                existingUser.PasswordHash = _passwordHasher.HashPassword(existingUser, definition.Password);
                await _userRepository.AddAsync(existingUser, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                result.CreatedUsers.Add(definition.Username);
                _logger.LogInformation("Created initial production identity user {Username}.", definition.Username);
            }
            else
            {
                result.SkippedExistingUsers.Add(definition.Username);
            }

            if (!await _userRoleRepository.ExistsAsync(existingUser.Id, adminRole.Id, cancellationToken))
            {
                await _userRoleRepository.AddAsync(existingUser.Id, adminRole.Id, now, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                result.AssignedAdminRoleUsers.Add(definition.Username);
            }
        }

        return result;
    }

    private async Task<AppRole> EnsureRoleAsync(
        string roleName,
        string description,
        DateTime now,
        InitialProductionIdentitySeedResult result,
        CancellationToken cancellationToken)
    {
        var normalizedName = roleName.ToUpperInvariant();
        var existing = await _roleRepository.GetByNormalizedNameAsync(normalizedName, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var role = new AppRole
        {
            Name = roleName,
            NormalizedName = normalizedName,
            Description = description,
            CreatedAtUtc = now
        };

        await _roleRepository.AddAsync(role, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        result.CreatedRoles.Add(roleName);
        _logger.LogInformation("Created initial production role {RoleName}.", roleName);
        return role;
    }

    private sealed record SeededUserDefinition(string Username, string DisplayName, string Password, string RoleName);
}

public sealed class InitialProductionIdentitySeedResult
{
    public List<string> CreatedRoles { get; } = [];

    public List<string> CreatedUsers { get; } = [];

    public List<string> AssignedAdminRoleUsers { get; } = [];

    public List<string> SkippedExistingUsers { get; } = [];
}
