using System.Data;
using MySqlConnector;
using Pineda.Facturacion.Application.Abstractions.Legacy;
using Pineda.Facturacion.Infrastructure.LegacyRead.Options;

namespace Pineda.Facturacion.Infrastructure.LegacyRead.Readers;

public sealed class LegacyOrderPaymentReader : ILegacyOrderPaymentReader
{
    private readonly string _connectionString;
    private readonly LegacySchemaResolver _schemaResolver = new();

    public LegacyOrderPaymentReader(LegacyReadOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new ArgumentException("Legacy read connection string is required.", nameof(options));
        }

        _connectionString = options.ConnectionString;
    }

    public async Task<LegacyOrderPaymentReadModel?> GetByOrderIdAsync(
        string legacyOrderId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(legacyOrderId))
        {
            return null;
        }

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var orders = await ResolveTableAsync(
            connection,
            "pedidos",
            ["noPedido", "cveVendedor"],
            cancellationToken);
        var vendors = await ResolveTableAsync(
            connection,
            "vendedores",
            ["cveVendedor", "Vendedor"],
            cancellationToken);

        await using var command = new MySqlCommand(BuildSql(orders, vendors), connection);
        command.Parameters.AddWithValue("@legacyOrderId", legacyOrderId.Trim());

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new LegacyOrderPaymentReadModel
        {
            LegacyPaymentCode = GetNullableTrimmedString(reader, "LegacyPaymentCode"),
            LegacyPaymentDescription = GetNullableTrimmedString(reader, "LegacyPaymentDescription")
        };
    }

    internal static string BuildSql(ResolvedLegacyTable orders, ResolvedLegacyTable vendors)
    {
        return $"""
            SELECT
                NULLIF(TRIM(p.{Q(orders["cveVendedor"])}), '') AS LegacyPaymentCode,
                NULLIF(TRIM(v.{Q(vendors["Vendedor"])}), '') AS LegacyPaymentDescription
            FROM {Q(orders.ActualName)} p
            LEFT JOIN {Q(vendors.ActualName)} v
                ON v.{Q(vendors["cveVendedor"])} = p.{Q(orders["cveVendedor"])}
            WHERE p.{Q(orders["noPedido"])} = @legacyOrderId
            LIMIT 1;
            """;
    }

    private async Task<ResolvedLegacyTable> ResolveTableAsync(
        MySqlConnection connection,
        string logicalTableName,
        IReadOnlyList<string> requiredColumns,
        CancellationToken cancellationToken)
    {
        var actualTableName = await _schemaResolver.ResolveTableAsync(connection, logicalTableName, cancellationToken);
        var columns = await _schemaResolver.ResolveColumnsAsync(
            connection,
            actualTableName,
            logicalTableName,
            requiredColumns,
            cancellationToken);
        return new ResolvedLegacyTable(logicalTableName, actualTableName, columns);
    }

    private static string? GetNullableTrimmedString(IDataRecord reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        var value = Convert.ToString(reader.GetValue(ordinal))?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string Q(string identifier)
    {
        return $"`{identifier.Replace("`", "``", StringComparison.Ordinal)}`";
    }
}
