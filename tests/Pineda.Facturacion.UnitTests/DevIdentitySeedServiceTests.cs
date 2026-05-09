using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Pineda.Facturacion.Application.Security;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Infrastructure.Options;
using Pineda.Facturacion.Infrastructure.Security;

namespace Pineda.Facturacion.UnitTests;

public sealed class DevIdentitySeedServiceTests
{
    [Fact]
    public async Task ExecuteAsync_DoesNothing_WhenDisabled()
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
            "Sandbox",
            new DevIdentitySeedOptions
            {
                Enabled = false,
                Users = DefaultUsers()
            });

        await service.ExecuteAsync();

        Assert.Empty(roleRepository.Stored);
        Assert.Empty(userRepository.Stored);
        Assert.Empty(userRoleRepository.Assignments);
        Assert.Empty(passwordHasher.HashCalls);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_FailsWithoutChanges_WhenEnabledOutsideAllowedEnvironment()
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
            "Production",
            EnabledOptions());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteAsync());

        Assert.Contains("not allowed", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(roleRepository.Stored);
        Assert.Empty(userRepository.Stored);
        Assert.Empty(userRoleRepository.Assignments);
        Assert.Empty(passwordHasher.HashCalls);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_FailsWithoutChanges_WhenEnabledWithoutDefaultPassword()
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
            "Sandbox",
            new DevIdentitySeedOptions
            {
                Enabled = true,
                DefaultPassword = "",
                Users = DefaultUsers()
            });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteAsync());

        Assert.Contains("DefaultPassword", exception.Message, StringComparison.Ordinal);
        Assert.Empty(roleRepository.Stored);
        Assert.Empty(userRepository.Stored);
        Assert.Empty(userRoleRepository.Assignments);
        Assert.Empty(passwordHasher.HashCalls);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_CreatesConfiguredUsers_InSandbox()
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
            "Sandbox",
            EnabledOptions());

        await service.ExecuteAsync();

        Assert.Single(roleRepository.Stored);
        Assert.Contains("ADMIN", roleRepository.Stored.Keys);
        Assert.Equal(2, userRepository.Stored.Count);
        Assert.Equal("Memo Aguirre", userRepository.Stored["MEMO.AGUIRRE"].DisplayName);
        Assert.Equal("Levi Lopez", userRepository.Stored["LEVI.LOPEZ"].DisplayName);
        Assert.All(userRepository.Stored.Values, user => Assert.True(user.IsActive));
        Assert.Equal("HASH::memo.aguirre::DevOnly123!", userRepository.Stored["MEMO.AGUIRRE"].PasswordHash);
        Assert.Equal("HASH::levi.lopez::DevOnly123!", userRepository.Stored["LEVI.LOPEZ"].PasswordHash);
        Assert.Equal(2, userRoleRepository.Assignments.Count);
        Assert.Contains(userRoleRepository.Assignments, assignment => assignment.Username == "memo.aguirre" && assignment.RoleName == AppRoleNames.Admin);
        Assert.Contains(userRoleRepository.Assignments, assignment => assignment.Username == "levi.lopez" && assignment.RoleName == AppRoleNames.Admin);
    }

    [Fact]
    public async Task ExecuteAsync_IsIdempotent_AndDoesNotDuplicateUsersRolesOrAssignments()
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
            "Development",
            EnabledOptions());

        await service.ExecuteAsync();
        await service.ExecuteAsync();

        Assert.Single(roleRepository.Stored);
        Assert.Equal(2, userRepository.Stored.Count);
        Assert.Equal(2, userRoleRepository.Assignments.Count);
        Assert.Equal(2, passwordHasher.HashCalls.Count);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotResetExistingPassword_WhenResetPasswordOnStartupIsFalse()
    {
        var existingUser = new AppUser
        {
            Id = 10,
            Username = "memo.aguirre",
            NormalizedUsername = "MEMO.AGUIRRE",
            DisplayName = "Memo Aguirre",
            PasswordHash = "OLD_HASH",
            IsActive = true,
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
            "Local",
            EnabledOptions(resetPasswordOnStartup: false));

        await service.ExecuteAsync();

        Assert.Equal("OLD_HASH", userRepository.Stored["MEMO.AGUIRRE"].PasswordHash);
        Assert.DoesNotContain(passwordHasher.HashCalls, call => call.Username == "memo.aguirre");
        Assert.Contains(passwordHasher.HashCalls, call => call.Username == "levi.lopez");
        Assert.Equal(2, userRoleRepository.Assignments.Count);
    }

    [Fact]
    public async Task ExecuteAsync_ResetsExistingPassword_WhenResetPasswordOnStartupIsTrue()
    {
        var existingUser = new AppUser
        {
            Id = 10,
            Username = "memo.aguirre",
            NormalizedUsername = "MEMO.AGUIRRE",
            DisplayName = "Memo Aguirre",
            PasswordHash = "OLD_HASH",
            IsActive = true,
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
            "Testing",
            EnabledOptions(resetPasswordOnStartup: true));

        await service.ExecuteAsync();

        Assert.Equal("HASH::memo.aguirre::DevOnly123!", userRepository.Stored["MEMO.AGUIRRE"].PasswordHash);
        Assert.Contains(passwordHasher.HashCalls, call => call.Username == "memo.aguirre");
        Assert.Contains(passwordHasher.HashCalls, call => call.Username == "levi.lopez");
    }

    [Fact]
    public async Task ExecuteAsync_AssignsConfiguredRoles()
    {
        var roleRepository = new BootstrapFakeAppRoleRepository();
        var userRepository = new BootstrapFakeAppUserRepository();
        var userRoleRepository = new BootstrapFakeAppUserRoleRepository();
        var service = CreateService(
            roleRepository,
            userRepository,
            userRoleRepository,
            new BootstrapFakePasswordHasher(),
            new BootstrapFakeUnitOfWork(),
            "Sandbox",
            new DevIdentitySeedOptions
            {
                Enabled = true,
                DefaultPassword = "DevOnly123!",
                Users =
                [
                    new DevIdentitySeedUserOptions
                    {
                        Username = "memo.aguirre",
                        DisplayName = "Memo Aguirre",
                        Roles = [AppRoleNames.Admin, AppRoleNames.Auditor]
                    },
                    new DevIdentitySeedUserOptions
                    {
                        Username = "levi.lopez",
                        DisplayName = "Levi Lopez",
                        Roles = [AppRoleNames.FiscalOperator]
                    }
                ]
            });

        await service.ExecuteAsync();

        Assert.Equal(3, roleRepository.Stored.Count);
        Assert.Equal(3, userRoleRepository.Assignments.Count);
        Assert.Contains(userRoleRepository.Assignments, assignment => assignment.Username == "memo.aguirre" && assignment.RoleName == AppRoleNames.Admin);
        Assert.Contains(userRoleRepository.Assignments, assignment => assignment.Username == "memo.aguirre" && assignment.RoleName == AppRoleNames.Auditor);
        Assert.Contains(userRoleRepository.Assignments, assignment => assignment.Username == "levi.lopez" && assignment.RoleName == AppRoleNames.FiscalOperator);
    }

    private static DevIdentitySeedService CreateService(
        BootstrapFakeAppRoleRepository roleRepository,
        BootstrapFakeAppUserRepository userRepository,
        BootstrapFakeAppUserRoleRepository userRoleRepository,
        BootstrapFakePasswordHasher passwordHasher,
        BootstrapFakeUnitOfWork unitOfWork,
        string environmentName,
        DevIdentitySeedOptions options)
    {
        userRoleRepository.Attach(userRepository, roleRepository);

        return new DevIdentitySeedService(
            roleRepository,
            userRepository,
            userRoleRepository,
            passwordHasher,
            unitOfWork,
            new BootstrapFakeHostEnvironment { EnvironmentName = environmentName },
            Options.Create(options),
            NullLogger<DevIdentitySeedService>.Instance);
    }

    private static DevIdentitySeedOptions EnabledOptions(bool resetPasswordOnStartup = false)
    {
        return new DevIdentitySeedOptions
        {
            Enabled = true,
            DefaultPassword = "DevOnly123!",
            ResetPasswordOnStartup = resetPasswordOnStartup,
            Users = DefaultUsers()
        };
    }

    private static List<DevIdentitySeedUserOptions> DefaultUsers()
    {
        return
        [
            new DevIdentitySeedUserOptions
            {
                Username = "memo.aguirre",
                DisplayName = "Memo Aguirre",
                Roles = [AppRoleNames.Admin]
            },
            new DevIdentitySeedUserOptions
            {
                Username = "levi.lopez",
                DisplayName = "Levi Lopez",
                Roles = [AppRoleNames.Admin]
            }
        ];
    }
}
