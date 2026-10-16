using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Pineda.Facturacion.Infrastructure.BillingWrite.Persistence;

namespace Pineda.Facturacion.Operations;

public static class ReceiverEmailSchemaMigration
{
    public const string TargetMigration = "20261016164158_ExpandFiscalReceiverEmailTo500";

    public static async Task<long> ApplyAsync(BillingDbContext db)
    {
        var applied = (await db.Database.GetAppliedMigrationsAsync()).ToHashSet(StringComparer.Ordinal);
        if (!applied.Contains(TargetMigration))
        {
            var pending = (await db.Database.GetPendingMigrationsAsync()).ToArray();
            if (pending.Length != 1 || pending[0] != TargetMigration)
            {
                Console.Error.WriteLine("Expected only the receiver-email migration. Pending: " + string.Join(", ", pending));
                throw new InvalidOperationException("Unexpected pending migrations; no schema changes applied.");
            }

            var currentLength = await ReadCapacityAsync(db);
            if (currentLength is not 200 and not 500)
            {
                throw new InvalidOperationException("Unexpected receiver email column capacity; no schema changes applied.");
            }

            await db.GetService<IMigrator>().MigrateAsync(TargetMigration);
        }

        var capacity = await ReadCapacityAsync(db);
        if (capacity < 500)
        {
            throw new InvalidOperationException("Receiver email column capacity verification failed.");
        }
        return capacity;
    }

    private static async Task<long> ReadCapacityAsync(BillingDbContext db)
    {
        await db.Database.OpenConnectionAsync();
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandTimeout = 30;
            command.CommandText = """
                SELECT CHARACTER_MAXIMUM_LENGTH
                FROM information_schema.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'fiscal_receiver'
                  AND COLUMN_NAME = 'email'
                  AND DATA_TYPE = 'varchar'
                """;
            return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }
}
