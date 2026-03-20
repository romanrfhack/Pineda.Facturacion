using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Pineda.Facturacion.Infrastructure.Security;

public sealed class AuthBootstrapHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;

    public AuthBootstrapHostedService(
        IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var bootstrapService = scope.ServiceProvider.GetRequiredService<IdentityBootstrapService>();
        await bootstrapService.ExecuteAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
