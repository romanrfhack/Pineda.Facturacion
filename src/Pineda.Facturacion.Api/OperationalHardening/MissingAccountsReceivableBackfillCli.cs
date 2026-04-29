using Pineda.Facturacion.Infrastructure.BillingWrite.Operations.AccountsReceivable;

namespace Pineda.Facturacion.Api.OperationalHardening;

internal static class MissingAccountsReceivableBackfillCli
{
    public const string CommandName = "backfill-missing-accounts-receivable-invoices";
    private const string ProductionGuardEnvironmentVariable = "ALLOW_PROD_MISSING_AR_BACKFILL";

    public static BackfillMissingAccountsReceivableInvoicesCommand Parse(string[] args)
    {
        var options = ParseNamedOptions(args);
        return new BackfillMissingAccountsReceivableInvoicesCommand
        {
            FiscalDocumentIds = ParseFiscalDocumentIds(GetOption(options, "--fiscal-document-ids")),
            CommitChanges = HasFlag(args, "--commit") || IsExplicitFalse(options, "--dry-run"),
            AllowProductionCommit = string.Equals(
                Environment.GetEnvironmentVariable(ProductionGuardEnvironmentVariable),
                "true",
                StringComparison.OrdinalIgnoreCase),
            BatchId = GetOption(options, "--batch-id"),
            ExpectedDatabaseName = GetOption(options, "--expected-database-name"),
            RequestedBy = GetOption(options, "--requested-by"),
            Notes = GetOption(options, "--notes")
        };
    }

    public static void WriteResult(BackfillMissingAccountsReceivableInvoicesResult result, string environmentName)
    {
        Console.WriteLine($"Environment: {environmentName}");
        Console.WriteLine($"Database: {result.DatabaseName ?? "(unknown)"}");
        Console.WriteLine($"Mode: {(result.CommitChanges ? "commit" : "dry-run")}");
        Console.WriteLine($"Batch id: {result.BatchId}");
        Console.WriteLine($"Evaluated: {result.EvaluatedCount}");
        Console.WriteLine($"Eligible: {result.EligibleCount}");
        Console.WriteLine($"Created: {result.CreatedCount}");
        Console.WriteLine($"Skipped: {result.SkippedCount}");
        Console.WriteLine($"Blocked: {result.BlockedCount}");
        Console.WriteLine("Affected entries:");
        foreach (var item in result.Items.OrderBy(x => x.FiscalDocumentId))
        {
            Console.WriteLine(
                $"- fiscalDocumentId={item.FiscalDocumentId} | invoiceId={item.AccountsReceivableInvoiceId?.ToString() ?? "null"} | decision={item.Decision} | outcome={item.Outcome} | action={item.PlannedAction} | total={FormatMoney(item.ProposedTotal)} | paid={FormatMoney(item.ProposedPaidTotal)} | outstanding={FormatMoney(item.ProposedOutstandingBalance)} | dueAtUtc={item.ProposedDueAtUtc:O}{FormatOptional(item.Message)}");
        }

        if (!result.IsSuccess && !string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            Console.WriteLine($"Error: {result.ErrorMessage}");
        }
    }

    private static IReadOnlyCollection<long> ParseFiscalDocumentIds(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            throw new InvalidOperationException("--fiscal-document-ids is required.");
        }

        var values = rawValue
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();
        if (values.Length == 0)
        {
            throw new InvalidOperationException("--fiscal-document-ids is required.");
        }

        var ids = new List<long>(values.Length);
        foreach (var value in values)
        {
            if (!long.TryParse(value, out var fiscalDocumentId) || fiscalDocumentId <= 0)
            {
                throw new InvalidOperationException($"Invalid fiscal document id '{value}'.");
            }

            ids.Add(fiscalDocumentId);
        }

        return ids
            .Distinct()
            .OrderBy(x => x)
            .ToArray();
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

    private static string FormatMoney(decimal? value)
    {
        return value.HasValue ? value.Value.ToString("0.00") : "null";
    }

    private static string FormatOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : $" | note={value}";
    }
}
