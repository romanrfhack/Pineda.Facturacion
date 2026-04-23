using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Abstractions.Security;
using Pineda.Facturacion.Application.Security;
using Pineda.Facturacion.Application.UseCases.Auth;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Infrastructure.Security;

namespace Pineda.Facturacion.UnitTests;

public class LoginServiceTests
{
    [Fact]
    public async Task ExecuteAsync_Succeeds_And_Resets_FailedLoginState()
    {
        var now = new DateTimeOffset(2026, 4, 11, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new MutableTimeProvider(now);
        var user = CreateUser("operator1", isActive: true);
        user.FailedLoginAttemptCount = 2;
        user.LastFailedLoginAtUtc = now.UtcDateTime.AddMinutes(-5);
        user.UpdatedAtUtc = now.UtcDateTime.AddMinutes(-5);
        user.UserRoles.Add(new AppUserRole
        {
            RoleId = 1,
            Role = new AppRole { Id = 1, Name = "FiscalOperator", NormalizedName = "FISCALOPERATOR" }
        });

        var repository = new FakeAppUserRepository(user);
        var auditService = new FakeAuditService();
        var unitOfWork = new FakeUnitOfWork();
        var passwordHasher = new FakePasswordHasher();
        var jwtTokenService = new FakeJwtTokenService();
        var service = CreateService(repository, passwordHasher, jwtTokenService, auditService, unitOfWork, timeProvider);

        var result = await service.ExecuteAsync(new LoginCommand
        {
            Username = "operator1",
            Password = "Secret123!",
            ClientIpAddress = "10.0.0.10"
        });

        Assert.Equal(LoginOutcome.Authenticated, result.Outcome);
        Assert.True(result.IsSuccess);
        Assert.Equal(0, user.FailedLoginAttemptCount);
        Assert.Null(user.LastFailedLoginAtUtc);
        Assert.Null(user.LockoutEndAtUtc);
        Assert.Equal(now.UtcDateTime, user.LastLoginAtUtc);
        Assert.Equal(now.UtcDateTime, user.UpdatedAtUtc);
        Assert.Single(passwordHasher.VerifyCalls);
        var auditRecord = Assert.Single(auditService.Records);
        Assert.Equal(LoginOutcome.Authenticated.ToString(), auditRecord.Outcome);
        Assert.Equal(1, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_LocksUser_AfterConfiguredThreshold()
    {
        var now = new DateTimeOffset(2026, 4, 11, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new MutableTimeProvider(now);
        var user = CreateUser("operator2", isActive: true);
        var repository = new FakeAppUserRepository(user);
        var auditService = new FakeAuditService();
        var unitOfWork = new FakeUnitOfWork();
        var passwordHasher = new FakePasswordHasher();
        var service = CreateService(repository, passwordHasher, new FakeJwtTokenService(), auditService, unitOfWork, timeProvider);

        for (var attempt = 1; attempt < LoginHardeningPolicy.MaxFailedAttempts; attempt++)
        {
            var result = await service.ExecuteAsync(new LoginCommand
            {
                Username = "operator2",
                Password = $"Wrong-{attempt}",
                ClientIpAddress = "10.0.0.11"
            });

            Assert.Equal(LoginOutcome.InvalidCredentials, result.Outcome);
        }

        var lockoutResult = await service.ExecuteAsync(new LoginCommand
        {
            Username = "operator2",
            Password = "Wrong-final",
            ClientIpAddress = "10.0.0.11"
        });

        Assert.Equal(LoginOutcome.LockedOut, lockoutResult.Outcome);
        Assert.False(lockoutResult.IsSuccess);
        Assert.Equal(LoginHardeningPolicy.MaxFailedAttempts, user.FailedLoginAttemptCount);
        Assert.Equal(now.UtcDateTime, user.LastFailedLoginAtUtc);
        Assert.Equal(now.UtcDateTime.Add(LoginHardeningPolicy.LockoutDuration), user.LockoutEndAtUtc);
        Assert.Equal(LoginOutcome.InvalidCredentials.ToString(), auditService.Records[^2].Outcome);
        Assert.Equal(LoginOutcome.LockedOut.ToString(), auditService.Records[^1].Outcome);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsLockedOut_WhileLockoutIsActive_WithoutRecheckingPassword()
    {
        var now = new DateTimeOffset(2026, 4, 11, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new MutableTimeProvider(now);
        var user = CreateUser("operator3", isActive: true);
        user.FailedLoginAttemptCount = LoginHardeningPolicy.MaxFailedAttempts;
        user.LastFailedLoginAtUtc = now.UtcDateTime.AddMinutes(-1);
        user.LockoutEndAtUtc = now.UtcDateTime.AddMinutes(10);

        var passwordHasher = new FakePasswordHasher();
        var auditService = new FakeAuditService();
        var service = CreateService(
            new FakeAppUserRepository(user),
            passwordHasher,
            new FakeJwtTokenService(),
            auditService,
            new FakeUnitOfWork(),
            timeProvider);

        var result = await service.ExecuteAsync(new LoginCommand
        {
            Username = "operator3",
            Password = "Secret123!",
            ClientIpAddress = "10.0.0.12"
        });

        Assert.Equal(LoginOutcome.LockedOut, result.Outcome);
        Assert.Equal(LoginHardeningPolicy.MaxFailedAttempts, user.FailedLoginAttemptCount);
        Assert.Equal(now.UtcDateTime.AddMinutes(10), user.LockoutEndAtUtc);
        Assert.Empty(passwordHasher.VerifyCalls);
        Assert.Single(auditService.Records);
        Assert.Equal(LoginOutcome.LockedOut.ToString(), auditService.Records[0].Outcome);
    }

    [Fact]
    public async Task ExecuteAsync_AllowsLogin_AfterExpiredLockout_And_AuditsExpiration()
    {
        var now = new DateTimeOffset(2026, 4, 11, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new MutableTimeProvider(now);
        var user = CreateUser("operator4", isActive: true);
        user.FailedLoginAttemptCount = LoginHardeningPolicy.MaxFailedAttempts;
        user.LastFailedLoginAtUtc = now.UtcDateTime.AddMinutes(-20);
        user.LockoutEndAtUtc = now.UtcDateTime.AddMinutes(-1);

        var auditService = new FakeAuditService();
        var unitOfWork = new FakeUnitOfWork();
        var service = CreateService(
            new FakeAppUserRepository(user),
            new FakePasswordHasher(),
            new FakeJwtTokenService(),
            auditService,
            unitOfWork,
            timeProvider);

        var result = await service.ExecuteAsync(new LoginCommand
        {
            Username = "operator4",
            Password = "Secret123!",
            ClientIpAddress = "10.0.0.13"
        });

        Assert.Equal(LoginOutcome.Authenticated, result.Outcome);
        Assert.True(result.IsSuccess);
        Assert.Equal(0, user.FailedLoginAttemptCount);
        Assert.Null(user.LastFailedLoginAtUtc);
        Assert.Null(user.LockoutEndAtUtc);
        Assert.Equal(now.UtcDateTime, user.LastLoginAtUtc);
        Assert.Equal(2, unitOfWork.SaveChangesCallCount);
        Assert.Equal(["LockoutExpired", LoginOutcome.Authenticated.ToString()], auditService.Records.Select(record => record.Outcome).ToArray());
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsGenericInvalidCredentials_ForUnknownUser()
    {
        var service = CreateService(
            new FakeAppUserRepository(),
            new FakePasswordHasher(),
            new FakeJwtTokenService(),
            new FakeAuditService(),
            new FakeUnitOfWork(),
            new MutableTimeProvider(new DateTimeOffset(2026, 4, 11, 12, 0, 0, TimeSpan.Zero)));

        var result = await service.ExecuteAsync(new LoginCommand
        {
            Username = "unknown-user",
            Password = "Whatever123!",
            ClientIpAddress = "10.0.0.14"
        });

        Assert.Equal(LoginOutcome.InvalidCredentials, result.Outcome);
        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid username or password.", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_ResetsUsernameIpThrottle_AfterSuccessfulLogin()
    {
        var now = new DateTimeOffset(2026, 4, 11, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new MutableTimeProvider(now);
        var throttleService = new InMemoryLoginAttemptThrottleService();
        var user = CreateUser("operator5", isActive: true);
        var repository = new FakeAppUserRepository(user);
        var auditService = new FakeAuditService();
        var service = CreateService(
            repository,
            new FakePasswordHasher(),
            new FakeJwtTokenService(),
            auditService,
            new FakeUnitOfWork(),
            timeProvider,
            throttleService);

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            var invalid = await service.ExecuteAsync(new LoginCommand
            {
                Username = "operator5",
                Password = $"Wrong-{attempt}",
                ClientIpAddress = "10.0.0.15"
            });

            Assert.Equal(LoginOutcome.InvalidCredentials, invalid.Outcome);
        }

        var success = await service.ExecuteAsync(new LoginCommand
        {
            Username = "operator5",
            Password = "Secret123!",
            ClientIpAddress = "10.0.0.15"
        });

        Assert.Equal(LoginOutcome.Authenticated, success.Outcome);

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            var invalid = await service.ExecuteAsync(new LoginCommand
            {
                Username = "operator5",
                Password = $"Wrong-reset-{attempt}",
                ClientIpAddress = "10.0.0.15"
            });

            Assert.Equal(LoginOutcome.InvalidCredentials, invalid.Outcome);
        }

        Assert.False(throttleService.Evaluate("OPERATOR5", "10.0.0.15", now.UtcDateTime).IsThrottled);
    }

    [Fact]
    public async Task ExecuteAsync_ThrottlesUnknownUsername_ByUsernameAndIp()
    {
        var now = new DateTimeOffset(2026, 4, 11, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new MutableTimeProvider(now);
        var auditService = new FakeAuditService();
        var service = CreateService(
            new FakeAppUserRepository(),
            new FakePasswordHasher(),
            new FakeJwtTokenService(),
            auditService,
            new FakeUnitOfWork(),
            timeProvider,
            new InMemoryLoginAttemptThrottleService());

        for (var attempt = 1; attempt < LoginHardeningPolicy.UsernameIpThrottleMaxFailedAttempts; attempt++)
        {
            var invalid = await service.ExecuteAsync(new LoginCommand
            {
                Username = "ghost-user",
                Password = $"Wrong-{attempt}",
                ClientIpAddress = "10.0.0.16"
            });

            Assert.Equal(LoginOutcome.InvalidCredentials, invalid.Outcome);
        }

        var throttled = await service.ExecuteAsync(new LoginCommand
        {
            Username = "ghost-user",
            Password = "Wrong-final",
            ClientIpAddress = "10.0.0.16"
        });

        var throttledAgain = await service.ExecuteAsync(new LoginCommand
        {
            Username = "ghost-user",
            Password = "Secret123!",
            ClientIpAddress = "10.0.0.16"
        });

        Assert.Equal(LoginOutcome.Throttled, throttled.Outcome);
        Assert.Equal(LoginOutcome.Throttled, throttledAgain.Outcome);
        Assert.Equal("Invalid username or password.", throttledAgain.ErrorMessage);
        Assert.Equal("ThrottledByUsernameIp", auditService.Records[^2].Outcome);
        Assert.Equal("ThrottledByUsernameIp", auditService.Records[^1].Outcome);
    }

    [Fact]
    public async Task ExecuteAsync_ThrottlesSameIp_AcrossMultipleUnknownUsernames_ByIp()
    {
        var now = new DateTimeOffset(2026, 4, 11, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new MutableTimeProvider(now);
        var auditService = new FakeAuditService();
        var service = CreateService(
            new FakeAppUserRepository(),
            new FakePasswordHasher(),
            new FakeJwtTokenService(),
            auditService,
            new FakeUnitOfWork(),
            timeProvider,
            new InMemoryLoginAttemptThrottleService());

        for (var attempt = 1; attempt < LoginHardeningPolicy.IpThrottleMaxFailedAttempts; attempt++)
        {
            var invalid = await service.ExecuteAsync(new LoginCommand
            {
                Username = $"ghost-{attempt}",
                Password = $"Wrong-{attempt}",
                ClientIpAddress = "10.0.0.17"
            });

            Assert.Equal(LoginOutcome.InvalidCredentials, invalid.Outcome);
        }

        var throttled = await service.ExecuteAsync(new LoginCommand
        {
            Username = "ghost-20",
            Password = "Wrong-final",
            ClientIpAddress = "10.0.0.17"
        });

        var throttledAgain = await service.ExecuteAsync(new LoginCommand
        {
            Username = "ghost-21",
            Password = "Wrong-next",
            ClientIpAddress = "10.0.0.17"
        });

        Assert.Equal(LoginOutcome.Throttled, throttled.Outcome);
        Assert.Equal(LoginOutcome.Throttled, throttledAgain.Outcome);
        Assert.Equal("ThrottledByIp", auditService.Records[^2].Outcome);
        Assert.Equal("ThrottledByIp", auditService.Records[^1].Outcome);
    }

    [Fact]
    public async Task ExecuteAsync_Coexists_WithPersistentUserLockout_And_LocalUsernameIpThrottle()
    {
        var now = new DateTimeOffset(2026, 4, 11, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new MutableTimeProvider(now);
        var throttleService = new InMemoryLoginAttemptThrottleService();
        var user = CreateUser("operator6", isActive: true);
        var passwordHasher = new FakePasswordHasher();
        var auditService = new FakeAuditService();
        var service = CreateService(
            new FakeAppUserRepository(user),
            passwordHasher,
            new FakeJwtTokenService(),
            auditService,
            new FakeUnitOfWork(),
            timeProvider,
            throttleService);

        for (var attempt = 1; attempt < LoginHardeningPolicy.MaxFailedAttempts; attempt++)
        {
            var invalid = await service.ExecuteAsync(new LoginCommand
            {
                Username = "operator6",
                Password = $"Wrong-{attempt}",
                ClientIpAddress = "10.0.0.18"
            });

            Assert.Equal(LoginOutcome.InvalidCredentials, invalid.Outcome);
        }

        var lockedOut = await service.ExecuteAsync(new LoginCommand
        {
            Username = "operator6",
            Password = "Wrong-final",
            ClientIpAddress = "10.0.0.18"
        });

        var throttled = await service.ExecuteAsync(new LoginCommand
        {
            Username = "operator6",
            Password = "Secret123!",
            ClientIpAddress = "10.0.0.18"
        });

        var throttleStatus = throttleService.Evaluate("OPERATOR6", "10.0.0.18", now.UtcDateTime);
        Assert.Equal(LoginOutcome.LockedOut, lockedOut.Outcome);
        Assert.Equal(LoginOutcome.Throttled, throttled.Outcome);
        Assert.NotNull(user.LockoutEndAtUtc);
        Assert.True(throttleStatus.IsThrottled);
        Assert.Equal(LoginAttemptThrottleScope.UsernameIp, throttleStatus.Scope);
        Assert.Equal(LoginHardeningPolicy.MaxFailedAttempts, passwordHasher.VerifyCalls.Count);
        Assert.Equal(LoginOutcome.LockedOut.ToString(), auditService.Records[^2].Outcome);
        Assert.Equal("ThrottledByUsernameIp", auditService.Records[^1].Outcome);
    }

    [Fact]
    public async Task ExecuteAsync_AllowsLogin_AfterLocalThrottleExpires_And_AuditsExpiration()
    {
        var now = new DateTimeOffset(2026, 4, 11, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new MutableTimeProvider(now);
        var throttleService = new InMemoryLoginAttemptThrottleService();
        var auditService = new FakeAuditService();
        var user = CreateUser("operator7", isActive: true);
        var service = CreateService(
            new FakeAppUserRepository(user),
            new FakePasswordHasher(),
            new FakeJwtTokenService(),
            auditService,
            new FakeUnitOfWork(),
            timeProvider,
            throttleService);

        for (var attempt = 1; attempt <= LoginHardeningPolicy.UsernameIpThrottleMaxFailedAttempts; attempt++)
        {
            throttleService.RegisterFailure("OPERATOR7", "10.0.0.19", now.UtcDateTime);
        }

        timeProvider.Advance(LoginHardeningPolicy.ThrottleDuration.Add(TimeSpan.FromSeconds(1)));

        var result = await service.ExecuteAsync(new LoginCommand
        {
            Username = "operator7",
            Password = "Secret123!",
            ClientIpAddress = "10.0.0.19"
        });

        Assert.Equal(LoginOutcome.Authenticated, result.Outcome);
        Assert.Equal(["ThrottleExpired", LoginOutcome.Authenticated.ToString()], auditService.Records.Select(record => record.Outcome).ToArray());
    }

    private static LoginService CreateService(
        FakeAppUserRepository repository,
        FakePasswordHasher passwordHasher,
        FakeJwtTokenService jwtTokenService,
        FakeAuditService auditService,
        FakeUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILoginAttemptThrottleService? loginAttemptThrottleService = null)
    {
        return new LoginService(
            repository,
            passwordHasher,
            jwtTokenService,
            loginAttemptThrottleService ?? new InMemoryLoginAttemptThrottleService(),
            auditService,
            unitOfWork,
            timeProvider);
    }

    private static AppUser CreateUser(string username, bool isActive)
    {
        var trimmedUsername = username.Trim();
        var normalizedUsername = trimmedUsername.ToUpperInvariant();

        return new AppUser
        {
            Id = 1,
            Username = trimmedUsername,
            NormalizedUsername = normalizedUsername,
            DisplayName = trimmedUsername,
            PasswordHash = $"HASH::{trimmedUsername}::Secret123!",
            IsActive = isActive,
            CreatedAtUtc = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)
        };
    }

    private sealed class FakeAppUserRepository : IAppUserRepository
    {
        private readonly Dictionary<string, AppUser> _users = [];

        public FakeAppUserRepository(params AppUser[] users)
        {
            foreach (var user in users)
            {
                _users[user.NormalizedUsername] = user;
            }
        }

        public Task<AppUser?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_users.Values.SingleOrDefault(user => user.Id == id));
        }

        public Task<AppUser?> GetByNormalizedUsernameAsync(string normalizedUsername, CancellationToken cancellationToken = default)
        {
            _users.TryGetValue(normalizedUsername, out var user);
            return Task.FromResult(user);
        }

        public Task<AppUser?> GetTrackedByNormalizedUsernameAsync(string normalizedUsername, CancellationToken cancellationToken = default)
        {
            _users.TryGetValue(normalizedUsername, out var user);
            return Task.FromResult(user);
        }

        public Task AddAsync(AppUser appUser, CancellationToken cancellationToken = default)
        {
            _users[appUser.NormalizedUsername] = appUser;
            return Task.CompletedTask;
        }
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public List<(string Username, string Password)> VerifyCalls { get; } = [];

        public string HashPassword(AppUser user, string password)
        {
            return $"HASH::{user.Username}::{password}";
        }

        public bool VerifyPassword(AppUser user, string password)
        {
            VerifyCalls.Add((user.Username, password));
            return string.Equals(user.PasswordHash, $"HASH::{user.Username}::{password}", StringComparison.Ordinal);
        }
    }

    private sealed class FakeJwtTokenService : IJwtTokenService
    {
        public JwtTokenResult CreateToken(AppUser user, IReadOnlyCollection<string> roles)
        {
            return new JwtTokenResult
            {
                Token = $"TOKEN::{user.Username}",
                ExpiresAtUtc = new DateTime(2026, 4, 11, 13, 0, 0, DateTimeKind.Utc)
            };
        }
    }

    private sealed class FakeAuditService : IAuditService
    {
        public List<AuditRecord> Records { get; } = [];

        public Task RecordAsync(AuditRecord record, CancellationToken cancellationToken = default)
        {
            Records.Add(record);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCallCount { get; private set; }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveChangesCallCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class MutableTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public MutableTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }

        public void Advance(TimeSpan value)
        {
            _utcNow = _utcNow.Add(value);
        }
    }
}
