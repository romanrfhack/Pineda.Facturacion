using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pineda.Facturacion.Infrastructure.BillingWrite.Options;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence;

public class BillingDbContextFactory : IDesignTimeDbContextFactory<BillingDbContext>
{
    public BillingDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(BillingWriteConnectionStringResolver.DesignTimeConnectionStringEnvironmentVariable);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = Environment.GetEnvironmentVariable(BillingWriteConnectionStringResolver.StandardConnectionStringEnvironmentVariable);
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = Environment.GetEnvironmentVariable(BillingWriteConnectionStringResolver.LegacyConnectionStringEnvironmentVariable);
        }

        connectionString ??= BillingWriteConnectionStringResolver.DesignTimeFallbackConnectionString;

        var optionsBuilder = new DbContextOptionsBuilder<BillingDbContext>();
        BillingDbContextOptionsConfigurator.Configure(optionsBuilder, connectionString);

        return new BillingDbContext(optionsBuilder.Options);
    }
}
