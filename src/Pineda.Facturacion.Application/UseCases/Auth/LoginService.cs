using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Abstractions.Security;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Application.Security;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.Auth;

public sealed class LoginService
{
    private const string AuthLoginActionType = "Auth.Login";
    private const string AppUserEntityType = "AppUser";
    private const string LoginAttemptThrottleEntityType = "LoginAttemptThrottle";

    private readonly IAppUserRepository _appUserRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ILoginAttemptThrottleService _loginAttemptThrottleService;
    private readonly IAuditService _auditService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public LoginService(
        IAppUserRepository appUserRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        ILoginAttemptThrottleService loginAttemptThrottleService,
        IAuditService auditService,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _appUserRepository = appUserRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _loginAttemptThrottleService = loginAttemptThrottleService;
        _auditService = auditService;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<LoginResult> ExecuteAsync(LoginCommand command, CancellationToken cancellationToken = default)
    {
        var normalizedUsername = FiscalMasterDataNormalization.NormalizeOptionalText(command.Username)?.ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalizedUsername) || string.IsNullOrWhiteSpace(command.Password))
        {
            await _auditService.RecordAsync(new AuditRecord
            {
                ActionType = AuthLoginActionType,
                EntityType = AppUserEntityType,
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

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var clientIpAddress = NormalizeClientIpAddress(command.ClientIpAddress);
        var throttleStatus = _loginAttemptThrottleService.Evaluate(normalizedUsername, clientIpAddress, now);
        await AuditThrottleExpirationAsync(command.Username, normalizedUsername, clientIpAddress, throttleStatus.ExpiredScopes, cancellationToken);

        if (throttleStatus.IsThrottled)
        {
            await _auditService.RecordAsync(new AuditRecord
            {
                ActionType = AuthLoginActionType,
                EntityType = LoginAttemptThrottleEntityType,
                EntityId = BuildThrottleEntityId(normalizedUsername, clientIpAddress, throttleStatus.Scope),
                Outcome = GetThrottleOutcome(throttleStatus.Scope),
                RequestSummary = new
                {
                    username = command.Username,
                    clientIpAddress
                },
                ResponseSummary = new
                {
                    throttleScope = throttleStatus.Scope?.ToString(),
                    throttleEndAtUtc = throttleStatus.ThrottleEndAtUtc,
                    clientIpAddress
                },
                ErrorMessage = "Login temporarily blocked due to repeated failed attempts.",
                ActorUsernameOverride = command.Username
            }, cancellationToken);

            return new LoginResult
            {
                Outcome = LoginOutcome.Throttled,
                IsSuccess = false,
                ErrorMessage = "Invalid username or password."
            };
        }

        var user = await _appUserRepository.GetTrackedByNormalizedUsernameAsync(normalizedUsername, cancellationToken);
        if (user is not null && IsLockoutExpired(user, now))
        {
            var expiredLockoutEndAtUtc = user.LockoutEndAtUtc;
            var failedAttemptCount = user.FailedLoginAttemptCount;

            ResetFailedLoginState(user, now);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _auditService.RecordAsync(new AuditRecord
            {
                ActionType = AuthLoginActionType,
                EntityType = AppUserEntityType,
                EntityId = user.Id.ToString(),
                Outcome = "LockoutExpired",
                RequestSummary = new { username = user.Username },
                ResponseSummary = new
                {
                    expiredLockoutEndAtUtc,
                    failedAttemptCount
                },
                ErrorMessage = "Previous temporary login lockout expired.",
                ActorUsernameOverride = user.Username
            }, cancellationToken);
        }

        if (user is not null && IsLockoutActive(user, now))
        {
            await _auditService.RecordAsync(new AuditRecord
            {
                ActionType = AuthLoginActionType,
                EntityType = AppUserEntityType,
                EntityId = user.Id.ToString(),
                Outcome = LoginOutcome.LockedOut.ToString(),
                RequestSummary = new { username = user.Username },
                ResponseSummary = new
                {
                    failedAttemptCount = user.FailedLoginAttemptCount,
                    lockoutEndAtUtc = user.LockoutEndAtUtc,
                    clientIpAddress
                },
                ErrorMessage = "Login temporarily blocked due to repeated failed attempts.",
                ActorUsernameOverride = user.Username
            }, cancellationToken);

            return new LoginResult
            {
                Outcome = LoginOutcome.LockedOut,
                IsSuccess = false,
                ErrorMessage = "Invalid username or password."
            };
        }

        if (user is null || !_passwordHasher.VerifyPassword(user, command.Password))
        {
            var outcome = LoginOutcome.InvalidCredentials;
            var auditOutcome = LoginOutcome.InvalidCredentials.ToString();
            string entityType = AppUserEntityType;
            string? entityId = normalizedUsername;
            DateTime? persistentLockoutEndAtUtc = null;
            int? failedAttemptCount = null;
            int? remainingAttempts = null;

            if (user is not null)
            {
                entityId = user.Id.ToString();
                user.FailedLoginAttemptCount++;
                user.LastFailedLoginAtUtc = now;
                user.UpdatedAtUtc = now;

                if (user.FailedLoginAttemptCount >= LoginHardeningPolicy.MaxFailedAttempts)
                {
                    user.LockoutEndAtUtc = now.Add(LoginHardeningPolicy.LockoutDuration);
                    outcome = LoginOutcome.LockedOut;
                    auditOutcome = LoginOutcome.LockedOut.ToString();
                }

                failedAttemptCount = user.FailedLoginAttemptCount;
                remainingAttempts = LoginHardeningPolicy.GetRemainingAttempts(user.FailedLoginAttemptCount);
                persistentLockoutEndAtUtc = user.LockoutEndAtUtc;

                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            var failureThrottleStatus = _loginAttemptThrottleService.RegisterFailure(normalizedUsername, clientIpAddress, now);
            if (outcome != LoginOutcome.LockedOut && failureThrottleStatus.IsThrottled)
            {
                outcome = LoginOutcome.Throttled;
                auditOutcome = GetThrottleOutcome(failureThrottleStatus.Scope);
                entityType = LoginAttemptThrottleEntityType;
                entityId = BuildThrottleEntityId(normalizedUsername, clientIpAddress, failureThrottleStatus.Scope);
            }

            await _auditService.RecordAsync(new AuditRecord
            {
                ActionType = AuthLoginActionType,
                EntityType = entityType,
                EntityId = entityId,
                Outcome = auditOutcome,
                RequestSummary = new
                {
                    username = command.Username,
                    clientIpAddress
                },
                ResponseSummary = new
                {
                    failedAttemptCount,
                    remainingAttempts,
                    lockoutEndAtUtc = persistentLockoutEndAtUtc,
                    throttleScope = failureThrottleStatus.Scope?.ToString(),
                    throttleEndAtUtc = failureThrottleStatus.ThrottleEndAtUtc,
                    clientIpAddress
                },
                ErrorMessage = outcome == LoginOutcome.LockedOut
                    ? "Login temporarily blocked due to repeated failed attempts."
                    : outcome == LoginOutcome.Throttled
                        ? "Login temporarily throttled due to repeated failed attempts."
                    : "Invalid username or password.",
                ActorUsernameOverride = command.Username
            }, cancellationToken);

            return new LoginResult
            {
                Outcome = outcome,
                IsSuccess = false,
                ErrorMessage = "Invalid username or password."
            };
        }

        if (!user.IsActive)
        {
            await _auditService.RecordAsync(new AuditRecord
            {
                ActionType = AuthLoginActionType,
                EntityType = AppUserEntityType,
                EntityId = user.Id.ToString(),
                Outcome = LoginOutcome.InactiveUser.ToString(),
                RequestSummary = new
                {
                    username = user.Username,
                    clientIpAddress
                },
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
        var resetFailedLoginState = user.FailedLoginAttemptCount > 0
            || user.LastFailedLoginAtUtc.HasValue
            || user.LockoutEndAtUtc.HasValue;
        var resetUsernameIpThrottle = _loginAttemptThrottleService.ResetUsernameIpThrottle(normalizedUsername, clientIpAddress);

        ResetFailedLoginState(user, now);
        user.LastLoginAtUtc = now;
        user.UpdatedAtUtc = now;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _auditService.RecordAsync(new AuditRecord
        {
            ActionType = AuthLoginActionType,
            EntityType = AppUserEntityType,
            EntityId = user.Id.ToString(),
            Outcome = LoginOutcome.Authenticated.ToString(),
            RequestSummary = new
            {
                username = user.Username,
                clientIpAddress
            },
            ResponseSummary = new
            {
                userId = user.Id,
                roles,
                resetFailedLoginState,
                resetUsernameIpThrottle,
                clientIpAddress
            },
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

    private static bool IsLockoutActive(AppUser user, DateTime now)
    {
        return user.LockoutEndAtUtc.HasValue && user.LockoutEndAtUtc.Value > now;
    }

    private static bool IsLockoutExpired(AppUser user, DateTime now)
    {
        return user.LockoutEndAtUtc.HasValue && user.LockoutEndAtUtc.Value <= now;
    }

    private static void ResetFailedLoginState(AppUser user, DateTime now)
    {
        user.FailedLoginAttemptCount = 0;
        user.LastFailedLoginAtUtc = null;
        user.LockoutEndAtUtc = null;
        user.UpdatedAtUtc = now;
    }

    private async Task AuditThrottleExpirationAsync(
        string? username,
        string normalizedUsername,
        string clientIpAddress,
        IReadOnlyList<LoginAttemptThrottleScope> expiredScopes,
        CancellationToken cancellationToken)
    {
        if (expiredScopes.Count == 0)
        {
            return;
        }

        await _auditService.RecordAsync(new AuditRecord
        {
            ActionType = AuthLoginActionType,
            EntityType = LoginAttemptThrottleEntityType,
            EntityId = BuildThrottleEntityId(normalizedUsername, clientIpAddress, scope: null),
            Outcome = "ThrottleExpired",
            RequestSummary = new
            {
                username,
                clientIpAddress
            },
            ResponseSummary = new
            {
                expiredScopes = expiredScopes.Select(scope => scope.ToString()).ToArray(),
                clientIpAddress
            },
            ErrorMessage = "Previous temporary login throttle expired.",
            ActorUsernameOverride = username
        }, cancellationToken);
    }

    private static string NormalizeClientIpAddress(string? clientIpAddress)
    {
        return string.IsNullOrWhiteSpace(clientIpAddress)
            ? "unknown"
            : clientIpAddress.Trim();
    }

    private static string GetThrottleOutcome(LoginAttemptThrottleScope? scope)
    {
        return scope switch
        {
            LoginAttemptThrottleScope.Ip => "ThrottledByIp",
            _ => "ThrottledByUsernameIp"
        };
    }

    private static string BuildThrottleEntityId(string normalizedUsername, string clientIpAddress, LoginAttemptThrottleScope? scope)
    {
        return scope == LoginAttemptThrottleScope.Ip
            ? clientIpAddress
            : $"{clientIpAddress}::{normalizedUsername}";
    }
}
