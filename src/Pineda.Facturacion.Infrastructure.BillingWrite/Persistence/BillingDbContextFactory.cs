using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence;

public class BillingDbContextFactory : IDesignTimeDbContextFactory<BillingDbContext>
{
    public const string MigrationsConnectionStringEnvironmentVariable = "PINEDA_FACTURACION_BILLINGWRITE_CONNECTION";

    private static readonly MySqlServerVersion ExplicitMySqlServerVersion = new(new Version(8, 0, 36));

    private const string FallbackConnectionString =
        "Server=127.0.0.1;Port=3306;Database=facturacion_v2_design_placeholder;User ID=root;Password=CHANGE_ME_FOR_MIGRATIONS_ONLY;";

    public BillingDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(MigrationsConnectionStringEnvironmentVariable);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = FallbackConnectionString;
        }

        var optionsBuilder = new DbContextOptionsBuilder<BillingDbContext>();
        optionsBuilder.UseMySql(
            connectionString,
            ExplicitMySqlServerVersion);

        return new BillingDbContext(optionsBuilder.Options);
    }
}
