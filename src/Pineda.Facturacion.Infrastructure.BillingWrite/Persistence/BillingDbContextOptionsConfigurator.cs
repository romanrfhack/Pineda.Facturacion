using Microsoft.EntityFrameworkCore;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence;

internal static class BillingDbContextOptionsConfigurator
{
    private static readonly MySqlServerVersion DesignTimeMySqlServerVersion = new(new Version(8, 0, 36));

    public static void Configure(DbContextOptionsBuilder optionsBuilder, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        if (EF.IsDesignTime)
        {
            optionsBuilder.UseMySql(connectionString, DesignTimeMySqlServerVersion);
            return;
        }

        optionsBuilder.UseMySql(
            connectionString,
            ServerVersion.AutoDetect(connectionString));
    }
}
