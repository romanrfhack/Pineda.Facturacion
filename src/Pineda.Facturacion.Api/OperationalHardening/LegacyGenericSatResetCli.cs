using Pineda.Facturacion.Infrastructure.BillingWrite.Operations.ProductFiscalProfiles;

namespace Pineda.Facturacion.Api.OperationalHardening;

internal static class LegacyGenericSatResetCli
{
    public const string ResetCommandName = "reset-legacy-generic-sat-assignments";
    public const string RollbackCommandName = "rollback-legacy-generic-sat-assignments";

    public static LegacyGenericSatResetCommand ParseReset(string[] args)
    {
        var options = ParseNamedOptions(args);
        return new LegacyGenericSatResetCommand
        {
            CommitChanges = HasFlag(args, "--commit") || IsExplicitFalse(options, "--dry-run"),
            AllowProductionCommit = string.Equals(
                Environment.GetEnvironmentVariable("ALLOW_PROD_SAT_GENERIC_RESET"),
                "true",
                StringComparison.OrdinalIgnoreCase),
            CleanupBatchId = GetOption(options, "--cleanup-batch-id"),
            ExpectedDatabaseName = GetOption(options, "--expected-database-name"),
            RequestedBy = GetOption(options, "--requested-by"),
            Notes = GetOption(options, "--notes")
        };
    }

    public static LegacyGenericSatResetRollbackCommand ParseRollback(string[] args)
    {
        var options = ParseNamedOptions(args);
        var cleanupBatchId = GetOption(options, "--cleanup-batch-id");
        if (string.IsNullOrWhiteSpace(cleanupBatchId))
        {
            throw new InvalidOperationException("--cleanup-batch-id is required for rollback.");
        }

        return new LegacyGenericSatResetRollbackCommand
        {
            CleanupBatchId = cleanupBatchId,
            AllowProductionCommit = string.Equals(
                Environment.GetEnvironmentVariable("ALLOW_PROD_SAT_GENERIC_RESET"),
                "true",
                StringComparison.OrdinalIgnoreCase),
            ExpectedDatabaseName = GetOption(options, "--expected-database-name")
        };
    }

    public static void WriteResetResult(LegacyGenericSatResetResult result, string environmentName)
    {
        Console.WriteLine($"Environment: {environmentName}");
        Console.WriteLine($"Database: {result.DatabaseName ?? "(unknown)"}");
        Console.WriteLine($"Mode: {(result.CommitChanges ? "commit" : "dry-run")}");
        Console.WriteLine($"Evaluated: {result.EvaluatedCount}");
        Console.WriteLine($"Eligible: {result.EligibleCount}");
        Console.WriteLine($"Updated: {result.UpdatedCount}");
        Console.WriteLine($"Skipped: {result.SkippedCount}");
        Console.WriteLine($"Excluded manual source: {result.ExcludedManualSourceCount}");
        Console.WriteLine($"Excluded import source: {result.ExcludedImportSourceCount}");
        Console.WriteLine($"Excluded by open manual source: {result.ExcludedByOpenManualSourceCount}");
        Console.WriteLine($"Excluded by open import source: {result.ExcludedByOpenImportSourceCount}");
        Console.WriteLine($"Excluded by historical manual source: {result.ExcludedByHistoricalManualSourceCount}");
        Console.WriteLine($"Excluded by historical import source: {result.ExcludedByHistoricalImportSourceCount}");
        Console.WriteLine($"Excluded manual audit: {result.ExcludedManualAuditCount}");
        Console.WriteLine($"Already pending: {result.AlreadyPendingCount}");
        Console.WriteLine($"Duplicate open assignments: {result.DuplicateOpenAssignmentCount}");
        if (!string.IsNullOrWhiteSpace(result.CleanupBatchId))
        {
            Console.WriteLine($"Cleanup batch id: {result.CleanupBatchId}");
        }

        if (result.DuplicateOpenAssignmentInternalCodes.Count > 0)
        {
            Console.WriteLine("Duplicate open assignment internal codes:");
            foreach (var internalCode in result.DuplicateOpenAssignmentInternalCodes)
            {
                Console.WriteLine($"- {internalCode}");
            }
        }

        Console.WriteLine("Affected entries:");
        foreach (var item in result.Items)
        {
            Console.WriteLine(
                $"- {item.InternalCode} | assignmentId={item.ProductFiscalAssignmentId?.ToString() ?? "null"} | profileId={item.ProductFiscalProfileId?.ToString() ?? "null"} | source={item.Source} | {item.PreviousReviewStatus ?? "(null)"}->{item.NewReviewStatus ?? "(null)"} | reason={item.PreviousReviewReason ?? "(null)"}->{item.NewReviewReason ?? "(null)"} | outcome={item.Outcome}{FormatOptional(item.SkipReason)}");
        }

        if (!result.IsSuccess && !string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            Console.WriteLine($"Error: {result.ErrorMessage}");
        }
    }

    public static void WriteRollbackResult(LegacyGenericSatResetRollbackResult result, string environmentName)
    {
        Console.WriteLine($"Environment: {environmentName}");
        Console.WriteLine($"Database: {result.DatabaseName ?? "(unknown)"}");
        Console.WriteLine($"Cleanup batch id: {result.CleanupBatchId}");
        Console.WriteLine($"Restored rows: {result.RestoredCount}");
        if (!result.IsSuccess && !string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            Console.WriteLine($"Error: {result.ErrorMessage}");
        }
    }

    private static Dictionary<string, string?> ParseNamedOptions(string[] args)
    {
        var options = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var argument in args)
        {
            if (!argument.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var separatorIndex = argument.IndexOf('=');
            if (separatorIndex < 0)
            {
                options[argument] = null;
                continue;
            }

            var key = argument[..separatorIndex];
            var value = argument[(separatorIndex + 1)..];
            options[key] = value;
        }

        return options;
    }

    private static string? GetOption(IReadOnlyDictionary<string, string?> options, string key)
    {
        return options.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;
    }

    private static bool HasFlag(IEnumerable<string> args, string key)
    {
        return args.Any(x => string.Equals(x, key, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsExplicitFalse(IReadOnlyDictionary<string, string?> options, string key)
    {
        return options.TryGetValue(key, out var value)
            && string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : $" | note={value}";
    }
}
