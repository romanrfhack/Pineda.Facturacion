using System.Globalization;
using MySqlConnector;

namespace Pineda.Facturacion.Infrastructure.LegacyRead.Readers;

internal sealed class LegacySchemaResolver
{
    private const int DiagnosticSampleLimit = 10;

    private readonly Dictionary<string, string> _resolvedTables = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyList<string>> _tableColumns = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<string>? _schemaTables;
    private string? _schemaName;

    public async Task<string> GetSchemaNameAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_schemaName))
        {
            return _schemaName;
        }

        await using var command = new MySqlCommand("SELECT DATABASE();", connection);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        var schemaName = Convert.ToString(result, CultureInfo.InvariantCulture);

        if (string.IsNullOrWhiteSpace(schemaName))
        {
            throw new InvalidOperationException("Unable to determine the current legacy schema name from DATABASE().");
        }

        _schemaName = schemaName;
        return schemaName;
    }

    public async Task<string> ResolveTableAsync(
        MySqlConnection connection,
        string logicalTableName,
        CancellationToken cancellationToken)
    {
        if (_resolvedTables.TryGetValue(logicalTableName, out var cachedTableName))
        {
            return cachedTableName;
        }

        var schemaName = await GetSchemaNameAsync(connection, cancellationToken);

        await using var command = new MySqlCommand(
            """
            SELECT TABLE_NAME
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = @schemaName
              AND LOWER(TABLE_NAME) = LOWER(@logicalTableName)
            ORDER BY TABLE_NAME;
            """,
            connection);
        command.Parameters.AddWithValue("@schemaName", schemaName);
        command.Parameters.AddWithValue("@logicalTableName", logicalTableName);

        var matches = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                matches.Add(reader.GetString(0));
            }
        }

        var resolvedTableName = SelectResolvedTableName(schemaName, logicalTableName, matches, await GetSchemaTablesAsync(connection, cancellationToken));
        _resolvedTables[logicalTableName] = resolvedTableName;
        return resolvedTableName;
    }

    public async Task<IReadOnlyDictionary<string, string>> ResolveColumnsAsync(
        MySqlConnection connection,
        string actualTableName,
        string logicalTableName,
        IEnumerable<string> logicalColumnNames,
        CancellationToken cancellationToken)
    {
        var actualColumns = await GetColumnsAsync(connection, actualTableName, cancellationToken);
        var schemaName = await GetSchemaNameAsync(connection, cancellationToken);
        var resolved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var logicalColumnName in logicalColumnNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            resolved[logicalColumnName] = SelectResolvedColumnName(
                schemaName,
                logicalTableName,
                actualTableName,
                [logicalColumnName],
                actualColumns);
        }

        return resolved;
    }

    public async Task<string> ResolveColumnAsync(
        MySqlConnection connection,
        string actualTableName,
        string logicalTableName,
        IReadOnlyList<string> candidateColumnNames,
        CancellationToken cancellationToken)
    {
        var actualColumns = await GetColumnsAsync(connection, actualTableName, cancellationToken);
        var schemaName = await GetSchemaNameAsync(connection, cancellationToken);

        return SelectResolvedColumnName(schemaName, logicalTableName, actualTableName, candidateColumnNames, actualColumns);
    }

    internal static string SelectResolvedTableName(
        string schemaName,
        string logicalTableName,
        IReadOnlyList<string> matches,
        IReadOnlyList<string> availableTables)
    {
        if (matches.Count == 0)
        {
            throw new InvalidOperationException(
                $"Legacy table resolution failed in schema '{schemaName}'. " +
                $"Logical table '{logicalTableName}' was not found using a case-insensitive lookup. " +
                $"Available tables sample: {FormatSample(availableTables)}.");
        }

        var exactMatch = matches.FirstOrDefault(match => string.Equals(match, logicalTableName, StringComparison.Ordinal));
        if (!string.IsNullOrWhiteSpace(exactMatch))
        {
            return exactMatch;
        }

        if (matches.Count == 1)
        {
            return matches[0];
        }

        throw new InvalidOperationException(
            $"Legacy table resolution is ambiguous in schema '{schemaName}'. " +
            $"Logical table '{logicalTableName}' matched multiple physical tables: {string.Join(", ", matches.Select(QuoteForMessage))}.");
    }

    internal static string SelectResolvedColumnName(
        string schemaName,
        string logicalTableName,
        string actualTableName,
        IReadOnlyList<string> candidateColumnNames,
        IReadOnlyList<string> actualColumns)
    {
        foreach (var candidate in candidateColumnNames)
        {
            var exactMatch = actualColumns.FirstOrDefault(column => string.Equals(column, candidate, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(exactMatch))
            {
                return exactMatch;
            }

            var caseInsensitiveMatches = actualColumns
                .Where(column => string.Equals(column, candidate, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (caseInsensitiveMatches.Length == 1)
            {
                return caseInsensitiveMatches[0];
            }

            if (caseInsensitiveMatches.Length > 1)
            {
                throw new InvalidOperationException(
                    $"Legacy column resolution is ambiguous in schema '{schemaName}'. " +
                    $"Logical table '{logicalTableName}' resolved to physical table '{actualTableName}', but candidate column '{candidate}' matched multiple physical columns: {string.Join(", ", caseInsensitiveMatches.Select(QuoteForMessage))}.");
            }
        }

        throw new InvalidOperationException(
            $"Legacy column resolution failed in schema '{schemaName}'. " +
            $"Logical table '{logicalTableName}' resolved to physical table '{actualTableName}', but none of the supported column candidates were found. " +
            $"Candidates: {string.Join(", ", candidateColumnNames.Select(QuoteForMessage))}. " +
            $"Available columns sample: {FormatSample(actualColumns)}.");
    }

    private async Task<IReadOnlyList<string>> GetSchemaTablesAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        if (_schemaTables is not null)
        {
            return _schemaTables;
        }

        var schemaName = await GetSchemaNameAsync(connection, cancellationToken);

        await using var command = new MySqlCommand(
            """
            SELECT TABLE_NAME
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = @schemaName
            ORDER BY TABLE_NAME;
            """,
            connection);
        command.Parameters.AddWithValue("@schemaName", schemaName);

        var tableNames = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                tableNames.Add(reader.GetString(0));
            }
        }

        _schemaTables = tableNames;
        return _schemaTables;
    }

    private async Task<IReadOnlyList<string>> GetColumnsAsync(
        MySqlConnection connection,
        string actualTableName,
        CancellationToken cancellationToken)
    {
        if (_tableColumns.TryGetValue(actualTableName, out var cachedColumns))
        {
            return cachedColumns;
        }

        var schemaName = await GetSchemaNameAsync(connection, cancellationToken);

        await using var command = new MySqlCommand(
            """
            SELECT COLUMN_NAME
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = @schemaName
              AND TABLE_NAME = @actualTableName
            ORDER BY ORDINAL_POSITION;
            """,
            connection);
        command.Parameters.AddWithValue("@schemaName", schemaName);
        command.Parameters.AddWithValue("@actualTableName", actualTableName);

        var columns = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                columns.Add(reader.GetString(0));
            }
        }

        _tableColumns[actualTableName] = columns;
        return columns;
    }

    private static string FormatSample(IReadOnlyList<string> values)
    {
        if (values.Count == 0)
        {
            return "(none)";
        }

        return string.Join(", ", values.Take(DiagnosticSampleLimit).Select(QuoteForMessage));
    }

    private static string QuoteForMessage(string value)
    {
        return $"'{value}'";
    }
}
