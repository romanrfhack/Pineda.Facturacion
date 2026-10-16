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

            // Match the active API's deployed production configuration and environment.
            // Never print credentials or pass them through command-line arguments.
            var entries = (await File.ReadAllTextAsync($"/proc/{pid}/environ")).Split('\0');
            var settingsPath = $"/proc/{pid}/cwd/appsettings.Production.json";
            var productionSettings = File.Exists(settingsPath) ? await File.ReadAllTextAsync(settingsPath) : null;
            var connectionString = ServiceBillingConnectionResolver.Resolve(entries, productionSettings);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                Console.Error.WriteLine("The running service has no billing connection in its environment or production settings. Migration refused.");
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
