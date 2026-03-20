using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Abstractions.Security;
using Pineda.Facturacion.Application.Security;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Infrastructure.Options;

namespace Pineda.Facturacion.Infrastructure.Security;

public sealed class AuthBootstrapHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IOptions<BootstrapAdminOptions> _bootstrapOptions;

    public AuthBootstrapHostedService(
        IServiceProvider serviceProvider,
        IHostEnvironment hostEnvironment,
        IOptions<BootstrapAdminOptions> bootstrapOptions)
    {
        _serviceProvider = serviceProvider;
        _hostEnvironment = hostEnvironment;
        _bootstrapOptions = bootstrapOptions;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var roleRepository = scope.ServiceProvider.GetRequiredService<IAppRoleRepository>();
        var userRepository = scope.ServiceProvider.GetRequiredService<IAppUserRepository>();
        var userRoleRepository = scope.ServiceProvider.GetRequiredService<IAppUserRoleRepository>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var createdAtUtc = DateTime.UtcNow;
        foreach (var roleName in AppRoleNames.All)
        {
            var normalizedName = roleName.ToUpperInvariant();
            var role = await roleRepository.GetByNormalizedNameAsync(normalizedName, cancellationToken);
            if (role is null)
            {
                await roleRepository.AddAsync(new AppRole
                {
                    Name = roleName,
                    NormalizedName = normalizedName,
                    CreatedAtUtc = createdAtUtc
                }, cancellationToken);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var options = _bootstrapOptions.Value;
        if (!options.Enabled || string.IsNullOrWhiteSpace(options.Username) || string.IsNullOrWhiteSpace(options.Password))
        {
            return;
        }

        if (!_hostEnvironment.IsDevelopment() && !_hostEnvironment.IsEnvironment("Local") && !_hostEnvironment.IsEnvironment("Testing"))
        {
            return;
        }

        var normalizedUsername = options.Username.Trim().ToUpperInvariant();
        var user = await userRepository.GetTrackedByNormalizedUsernameAsync(normalizedUsername, cancellationToken);
        if (user is null)
        {
            user = new AppUser
            {
                Username = options.Username.Trim(),
                NormalizedUsername = normalizedUsername,
                DisplayName = string.IsNullOrWhiteSpace(options.DisplayName) ? options.Username.Trim() : options.DisplayName.Trim(),
                IsActive = true,
                CreatedAtUtc = createdAtUtc,
                UpdatedAtUtc = createdAtUtc
            };
            user.PasswordHash = passwordHasher.HashPassword(user, options.Password);
            await userRepository.AddAsync(user, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var adminRole = await roleRepository.GetByNormalizedNameAsync(AppRoleNames.Admin.ToUpperInvariant(), cancellationToken);
        if (adminRole is not null && !await userRoleRepository.ExistsAsync(user.Id, adminRole.Id, cancellationToken))
        {
            await userRoleRepository.AddAsync(user.Id, adminRole.Id, createdAtUtc, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
