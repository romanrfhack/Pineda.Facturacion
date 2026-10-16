using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Infrastructure.BillingWrite.Persistence;

namespace Pineda.Facturacion.Operations;

internal static class EmailMigrationProgram
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Length != 2 || args[0] != "--service-pid" || !int.TryParse(args[1], out var pid) || pid <= 0)
            {
                Console.Error.WriteLine("Expected --service-pid with the running facturas-api MainPID.");
                return 1;
            }

            var commandLine = await File.ReadAllTextAsync($"/proc/{pid}/cmdline");
            if (!commandLine.Split('\0').Any(value => Path.GetFileName(value) == "Pineda.Facturacion.Api.dll"))
            {
                Console.Error.WriteLine("The process is not the billing API. Migration refused.");
                return 1;
            }

            // Read only the billing connection from the running service; never print secrets
            // or pass them through command-line arguments or GitHub Actions outputs.
            var entries = (await File.ReadAllTextAsync($"/proc/{pid}/environ")).Split('\0');
            string? ReadSetting(string key) => entries.FirstOrDefault(value => value.StartsWith(key + "=", StringComparison.Ordinal))?[(key.Length + 1)..];
            var connectionString = ReadSetting("ConnectionStrings__BillingWrite");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                connectionString = ReadSetting("BillingWrite__ConnectionString");
            }
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                Console.Error.WriteLine("The running service has no billing connection in its environment. Migration refused.");
                return 1;
            }

            var options = new DbContextOptionsBuilder<BillingDbContext>()
                .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString), mysql => mysql.CommandTimeout(60))
                .Options;
            await using var db = new BillingDbContext(options);
            var length = await ReceiverEmailSchemaMigration.ApplyAsync(db);
            Console.WriteLine($"Verified {ReceiverEmailSchemaMigration.TargetMigration}: fiscal_receiver.email capacity={length}.");
            return 0;
        }
        catch (Exception exception)
        {
            // Avoid leaking runtime configuration through exception messages/stack traces.
            Console.Error.WriteLine($"Receiver email migration failed ({exception.GetType().Name}). Deployment stopped.");
            return 1;
        }
    }
}
