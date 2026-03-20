using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pineda.Facturacion.Infrastructure.BillingWrite.Persistence;
using Pineda.Facturacion.Infrastructure.Options;

namespace Pineda.Facturacion.Infrastructure.Security;

public sealed class DatabaseBootstrapHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IOptions<BootstrapSeedOptions> _bootstrapOptions;
    private readonly ILogger<DatabaseBootstrapHostedService> _logger;

    public DatabaseBootstrapHostedService(
        IServiceProvider serviceProvider,
        IHostEnvironment hostEnvironment,
        IOptions<BootstrapSeedOptions> bootstrapOptions,
        ILogger<DatabaseBootstrapHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _hostEnvironment = hostEnvironment;
        _bootstrapOptions = bootstrapOptions;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_bootstrapOptions.Value.ApplyMigrationsOnStartup || !IsNonProductionBootstrapEnvironment())
        {
            return;
        }

        await using var scope = _serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BillingDbContext>();

        if (!dbContext.Database.IsRelational())
        {
            _logger.LogInformation(
                "Skipping startup migration bootstrap for environment {EnvironmentName} because the configured database provider is not relational.",
                _hostEnvironment.EnvironmentName);
            return;
        }

        _logger.LogInformation("Applying BillingWrite migrations on startup for environment {EnvironmentName}.", _hostEnvironment.EnvironmentName);
        await dbContext.Database.MigrateAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private bool IsNonProductionBootstrapEnvironment()
    {
        return _hostEnvironment.IsDevelopment()
            || _hostEnvironment.IsEnvironment("Local")
            || _hostEnvironment.IsEnvironment("Testing")
            || _hostEnvironment.IsEnvironment("Sandbox");
    }
}
