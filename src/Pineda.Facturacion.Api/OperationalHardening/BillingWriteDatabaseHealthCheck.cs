using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Pineda.Facturacion.Infrastructure.BillingWrite.Persistence;

namespace Pineda.Facturacion.Api.OperationalHardening;

internal sealed class BillingWriteDatabaseHealthCheck : IHealthCheck
{
    private readonly BillingDbContext _dbContext;

    public BillingWriteDatabaseHealthCheck(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!_dbContext.Database.IsRelational())
        {
            return HealthCheckResult.Healthy("BillingWrite uses a non-relational provider in this environment.");
        }

        try
        {
            var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy("BillingWrite database is reachable.")
                : HealthCheckResult.Unhealthy("BillingWrite database is not reachable.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("BillingWrite database health check failed.", exception);
        }
    }
}
