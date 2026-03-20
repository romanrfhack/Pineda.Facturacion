using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Abstractions.Security;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Application.Security;

namespace Pineda.Facturacion.Application.UseCases.Auth;

public sealed class LoginService
{
    private readonly IAppUserRepository _appUserRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IAuditService _auditService;
    private readonly IUnitOfWork _unitOfWork;

    public LoginService(
        IAppUserRepository appUserRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IAuditService auditService,
        IUnitOfWork unitOfWork)
    {
        _appUserRepository = appUserRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _auditService = auditService;
        _unitOfWork = unitOfWork;
    }

    public async Task<LoginResult> ExecuteAsync(LoginCommand command, CancellationToken cancellationToken = default)
    {
        var normalizedUsername = FiscalMasterDataNormalization.NormalizeOptionalText(command.Username)?.ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalizedUsername) || string.IsNullOrWhiteSpace(command.Password))
        {
            await _auditService.RecordAsync(new AuditRecord
            {
                ActionType = "Auth.Login",
                EntityType = "AppUser",
                EntityId = normalizedUsername,
                Outcome = LoginOutcome.ValidationFailed.ToString(),
                RequestSummary = new { username = command.Username },
                ErrorMessage = "Username and password are required.",
                ActorUsernameOverride = command.Username
            }, cancellationToken);

            return new LoginResult
            {
                Outcome = LoginOutcome.ValidationFailed,
                IsSuccess = false,
                ErrorMessage = "Username and password are required."
            };
        }

        var user = await _appUserRepository.GetTrackedByNormalizedUsernameAsync(normalizedUsername, cancellationToken);
        if (user is null || !_passwordHasher.VerifyPassword(user, command.Password))
        {
            await _auditService.RecordAsync(new AuditRecord
            {
                ActionType = "Auth.Login",
                EntityType = "AppUser",
                EntityId = normalizedUsername,
                Outcome = LoginOutcome.InvalidCredentials.ToString(),
                RequestSummary = new { username = command.Username },
                ErrorMessage = "Invalid username or password.",
                ActorUsernameOverride = command.Username
            }, cancellationToken);

            return new LoginResult
            {
                Outcome = LoginOutcome.InvalidCredentials,
                IsSuccess = false,
                ErrorMessage = "Invalid username or password."
            };
        }

        if (!user.IsActive)
        {
            await _auditService.RecordAsync(new AuditRecord
            {
                ActionType = "Auth.Login",
                EntityType = "AppUser",
                EntityId = user.Id.ToString(),
                Outcome = LoginOutcome.InactiveUser.ToString(),
                RequestSummary = new { username = user.Username },
                ErrorMessage = "User is inactive.",
                ActorUsernameOverride = user.Username
            }, cancellationToken);

            return new LoginResult
            {
                Outcome = LoginOutcome.InactiveUser,
                IsSuccess = false,
                ErrorMessage = "User is inactive."
            };
        }

        var roles = user.UserRoles
            .Where(x => x.Role is not null)
            .Select(x => x.Role!.Name)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        var token = _jwtTokenService.CreateToken(user, roles);
        user.LastLoginAtUtc = DateTime.UtcNow;
        user.UpdatedAtUtc = user.LastLoginAtUtc.Value;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditService.RecordAsync(new AuditRecord
        {
            ActionType = "Auth.Login",
            EntityType = "AppUser",
            EntityId = user.Id.ToString(),
            Outcome = LoginOutcome.Authenticated.ToString(),
            RequestSummary = new { username = user.Username },
            ResponseSummary = new { userId = user.Id, roles },
            ActorUsernameOverride = user.Username
        }, cancellationToken);

        return new LoginResult
        {
            Outcome = LoginOutcome.Authenticated,
            IsSuccess = true,
            UserId = user.Id,
            Username = user.Username,
            DisplayName = user.DisplayName,
            Roles = roles,
            Token = token.Token,
            ExpiresAtUtc = token.ExpiresAtUtc
        };
    }
}
