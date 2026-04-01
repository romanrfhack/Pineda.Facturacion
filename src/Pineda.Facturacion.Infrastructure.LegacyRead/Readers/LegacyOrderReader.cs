using System.Data;
using System.Globalization;
using MySqlConnector;
using Pineda.Facturacion.Application.Abstractions.Legacy;
using Pineda.Facturacion.Application.Models.Legacy;
using Pineda.Facturacion.Infrastructure.LegacyRead.Options;

namespace Pineda.Facturacion.Infrastructure.LegacyRead.Readers;

public class LegacyOrderReader : ILegacyOrderReader
{
    private static readonly string[] OrderDateColumnCandidates = ["Fecha", "FechaPedido", "FecPedido", "fecha", "fechaPedido"];

    private readonly string _connectionString;
    private readonly LegacySchemaResolver _schemaResolver = new();
    private LegacyOrderReadSchema? _schema;

    public LegacyOrderReader(LegacyReadOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new ArgumentException("Legacy read connection string is required.", nameof(options));
        }

        _connectionString = options.ConnectionString;
    }

    public async Task<LegacyOrderReadModel?> GetByIdAsync(string legacyOrderId, CancellationToken cancellationToken = default)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var schema = await ResolveSchemaAsync(connection, cancellationToken);

        var header = await ReadHeaderAsync(connection, schema, legacyOrderId, cancellationToken);

        if (header is null)
        {
            return null;
        }

        header.Items = await ReadItemsAsync(connection, schema, legacyOrderId, cancellationToken);
        return header;
    }

    public async Task<LegacyOrderPageReadModel> SearchAsync(LegacyOrderSearchReadModel search, CancellationToken cancellationToken = default)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var schema = await ResolveSchemaAsync(connection, cancellationToken);

        var countSql = BuildCountSql(schema);
        var listSql = BuildListSql(schema);

        var totalCount = await CountAsync(connection, countSql, search, cancellationToken);
        var items = await ReadPageAsync(connection, listSql, search, cancellationToken);

        return new LegacyOrderPageReadModel
        {
            Items = items,
            TotalCount = totalCount,
            Page = search.Page,
            PageSize = search.PageSize
        };
    }

    private static async Task<LegacyOrderReadModel?> ReadHeaderAsync(
        MySqlConnection connection,
        LegacyOrderReadSchema schema,
        string legacyOrderId,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(BuildHeaderSql(schema), connection);
        command.Parameters.AddWithValue("@legacyOrderId", legacyOrderId);

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new LegacyOrderReadModel
        {
            LegacyOrderId = GetRequiredString(reader, "LegacyOrderId"),
            LegacyOrderNumber = GetRequiredString(reader, "LegacyOrderNumber"),
            LegacyOrderType = GetNullableString(reader, "LegacyOrderType"),
            CustomerLegacyId = GetRequiredString(reader, "CustomerLegacyId"),
            CustomerName = GetRequiredString(reader, "CustomerName"),
            CustomerRfc = GetNullableString(reader, "CustomerRfc"),
            PaymentCondition = GetRequiredString(reader, "PaymentCondition"),
            PriceListCode = GetNullableString(reader, "PriceListCode"),
            DeliveryType = GetNullableString(reader, "DeliveryType"),
            CurrencyCode = "MXN",
            Subtotal = GetRequiredDecimal(reader, "Subtotal"),
            DiscountTotal = GetRequiredDecimal(reader, "DiscountTotal"),
            TaxTotal = GetRequiredDecimal(reader, "TaxTotal"),
            Total = GetRequiredDecimal(reader, "Total")
        };
    }

    private static async Task<List<LegacyOrderItemReadModel>> ReadItemsAsync(
        MySqlConnection connection,
        LegacyOrderReadSchema schema,
        string legacyOrderId,
        CancellationToken cancellationToken)
    {
        var items = new List<LegacyOrderItemReadModel>();

        await using var command = new MySqlCommand(BuildDetailSql(schema), connection);
        command.Parameters.AddWithValue("@legacyOrderId", legacyOrderId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var lineNumber = 1;

        while (await reader.ReadAsync(cancellationToken))
        {
            var quantity = GetRequiredDecimal(reader, "Quantity");
            var grossEffectiveUnitPrice = GetRequiredDecimal(reader, "GrossEffectiveUnitPrice");

            items.Add(new LegacyOrderItemReadModel
            {
                LineNumber = lineNumber++,
                LegacyArticleId = GetRequiredString(reader, "LegacyArticleId"),
                // docs/013 documents Sku as a temporary same-source mapping from cveArticulo.
                Sku = GetNullableString(reader, "Sku"),
                Description = BuildDescription(
                    GetNullableString(reader, "NomArt"),
                    GetNullableString(reader, "ArticleName"),
                    GetNullableString(reader, "ArticleSpecification"),
                    GetNullableString(reader, "LegacyArticleId")),
                UnitCode = GetNullableString(reader, "UnitCode"),
                UnitName = GetNullableString(reader, "UnitName"),
                Quantity = quantity,
                UnitPrice = NormalizeGrossAmountToNet(grossEffectiveUnitPrice),
                DiscountAmount = 0m,
                TaxRate = GetRequiredDecimal(reader, "TaxRate"),
                TaxAmount = GetRequiredDecimal(reader, "TaxAmount"),
                LineTotal = NormalizeGrossAmountToNet(grossEffectiveUnitPrice * quantity),
                // docs/013 maps SAT product/service code to TBD, so this remains intentionally unmapped.
                // docs/013 maps SAT unit code to TBD, so this remains intentionally unmapped.
            });
        }

        return items;
    }

    private async Task<LegacyOrderReadSchema> ResolveSchemaAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        if (_schema is not null)
        {
            return _schema;
        }

        var orders = await ResolveTableAsync(
            connection,
            "pedidos",
            ["noPedido", "refPedido", "TipoPedido", "noCliente", "condPagoPedido", "TipoEntrega", "MontoPedido"],
            cancellationToken);
        var customers = await ResolveTableAsync(
            connection,
            "clientes",
            ["XRazonSocial", "Nombre", "Paterno", "Materno", "Cliente", "RFC", "TipoCliente", "noCliente"],
            cancellationToken);
        var orderItems = await ResolveTableAsync(
            connection,
            "pedidosdet",
            ["SuPrecio", "Cantidad", "noPedido", "cveArticulo", "cveMarcaArticulo", "uniMedida"],
            cancellationToken);
        var articles = await ResolveTableAsync(
            connection,
            "articulos",
            ["cveArticulo", "cveMarcaArticulo", "Articulo", "Especificacion", "uniMedida", "cveNomArt", "cveCategoria", "cveGrupo"],
            cancellationToken);
        var articleNames = await ResolveTableAsync(
            connection,
            "nombresarticulos",
            ["NomArt", "cveNomArt", "cveCategoria", "cveGrupo"],
            cancellationToken);
        var orderDateColumn = await _schemaResolver.ResolveColumnAsync(
            connection,
            orders.ActualName,
            orders.LogicalName,
            OrderDateColumnCandidates,
            cancellationToken);

        _schema = new LegacyOrderReadSchema(orders, customers, orderItems, articles, articleNames, orderDateColumn);
        return _schema;
    }

    private async Task<ResolvedLegacyTable> ResolveTableAsync(
        MySqlConnection connection,
        string logicalTableName,
        IReadOnlyList<string> requiredColumns,
        CancellationToken cancellationToken)
    {
        var actualTableName = await _schemaResolver.ResolveTableAsync(connection, logicalTableName, cancellationToken);
        var columns = await _schemaResolver.ResolveColumnsAsync(connection, actualTableName, logicalTableName, requiredColumns, cancellationToken);
        return new ResolvedLegacyTable(logicalTableName, actualTableName, columns);
    }

    internal static string BuildHeaderSql(LegacyOrderReadSchema schema)
    {
        return $"""
            SELECT
                p.{Q(schema.Orders["noPedido"])} AS LegacyOrderId,
                p.{Q(schema.Orders["refPedido"])} AS LegacyOrderNumber,
                p.{Q(schema.Orders["TipoPedido"])} AS LegacyOrderType,
                p.{Q(schema.Orders["noCliente"])} AS CustomerLegacyId,
                COALESCE(
                    NULLIF(TRIM(c.{Q(schema.Customers["XRazonSocial"])}), ''),
                    NULLIF(TRIM(CONCAT_WS(' ', NULLIF(c.{Q(schema.Customers["Nombre"])}, ''), NULLIF(c.{Q(schema.Customers["Paterno"])}, ''), NULLIF(c.{Q(schema.Customers["Materno"])}, ''))), ''),
                    NULLIF(TRIM(c.{Q(schema.Customers["Cliente"])}), ''),
                    CONCAT('Cliente ', p.{Q(schema.Orders["noCliente"])})
                ) AS CustomerName,
                c.{Q(schema.Customers["RFC"])} AS CustomerRfc,
                p.{Q(schema.Orders["condPagoPedido"])} AS PaymentCondition,
                c.{Q(schema.Customers["TipoCliente"])} AS PriceListCode,
                p.{Q(schema.Orders["TipoEntrega"])} AS DeliveryType,
                COALESCE((
                    SELECT SUM(d.{Q(schema.OrderItems["SuPrecio"])} * d.{Q(schema.OrderItems["Cantidad"])})
                    FROM {Q(schema.OrderItems.ActualName)} d
                    WHERE d.{Q(schema.OrderItems["noPedido"])} = p.{Q(schema.Orders["noPedido"])}
                ), 0) AS Subtotal,
                0 AS DiscountTotal,
                0 AS TaxTotal,
                p.{Q(schema.Orders["MontoPedido"])} AS Total
            FROM {Q(schema.Orders.ActualName)} p
            INNER JOIN {Q(schema.Customers.ActualName)} c
                ON p.{Q(schema.Orders["noCliente"])} = c.{Q(schema.Customers["noCliente"])}
            WHERE p.{Q(schema.Orders["noPedido"])} = @legacyOrderId
              AND p.{Q(schema.Orders["noCliente"])} <> 0
              AND p.{Q(schema.Orders["MontoPedido"])} <> 0.00
              AND p.{Q(schema.Orders["refPedido"])} IS NOT NULL
            LIMIT 1;
            """;
    }

    internal static string BuildDetailSql(LegacyOrderReadSchema schema)
    {
        return $"""
            SELECT
                d.{Q(schema.OrderItems["cveArticulo"])} AS LegacyArticleId,
                d.{Q(schema.OrderItems["cveArticulo"])} AS Sku,
                n.{Q(schema.ArticleNames["NomArt"])} AS NomArt,
                a.{Q(schema.Articles["Articulo"])} AS ArticleName,
                a.{Q(schema.Articles["Especificacion"])} AS ArticleSpecification,
                d.{Q(schema.OrderItems["uniMedida"])} AS UnitCode,
                a.{Q(schema.Articles["uniMedida"])} AS UnitName,
                d.{Q(schema.OrderItems["Cantidad"])} AS Quantity,
                d.{Q(schema.OrderItems["SuPrecio"])} AS GrossEffectiveUnitPrice,
                0 AS TaxRate,
                0 AS TaxAmount,
                0 AS LineTotal
            FROM {Q(schema.OrderItems.ActualName)} d
            INNER JOIN {Q(schema.Articles.ActualName)} a
                ON d.{Q(schema.OrderItems["cveArticulo"])} = a.{Q(schema.Articles["cveArticulo"])}
               AND d.{Q(schema.OrderItems["cveMarcaArticulo"])} = a.{Q(schema.Articles["cveMarcaArticulo"])}
            LEFT JOIN {Q(schema.ArticleNames.ActualName)} n
                ON a.{Q(schema.Articles["cveNomArt"])} = n.{Q(schema.ArticleNames["cveNomArt"])}
               AND a.{Q(schema.Articles["cveCategoria"])} = n.{Q(schema.ArticleNames["cveCategoria"])}
               AND a.{Q(schema.Articles["cveGrupo"])} = n.{Q(schema.ArticleNames["cveGrupo"])}
            WHERE d.{Q(schema.OrderItems["noPedido"])} = @legacyOrderId
            ORDER BY d.{Q(schema.OrderItems["cveArticulo"])}, d.{Q(schema.OrderItems["cveMarcaArticulo"])}, d.{Q(schema.OrderItems["SuPrecio"])}, d.{Q(schema.OrderItems["Cantidad"])}, d.{Q(schema.OrderItems["uniMedida"])};
            """;
    }

    internal static string BuildCountSql(LegacyOrderReadSchema schema)
    {
        return $"""
            SELECT COUNT(*)
            FROM {Q(schema.Orders.ActualName)} p
            INNER JOIN {Q(schema.Customers.ActualName)} c
                ON p.{Q(schema.Orders["noCliente"])} = c.{Q(schema.Customers["noCliente"])}
            WHERE p.{Q(schema.OrderDateColumn)} >= @fromDateUtc
              AND p.{Q(schema.OrderDateColumn)} < @toDateUtcExclusive
              AND (@customerQueryLike IS NULL OR UPPER({BuildCustomerNameExpression(schema)}) LIKE @customerQueryLike)
              AND p.{Q(schema.Orders["noCliente"])} <> 0
              AND p.{Q(schema.Orders["MontoPedido"])} <> 0.00
              AND p.{Q(schema.Orders["refPedido"])} IS NOT NULL;
            """;
    }

    internal static string BuildListSql(LegacyOrderReadSchema schema)
    {
        return $"""
            SELECT
                p.{Q(schema.Orders["noPedido"])} AS LegacyOrderId,
                p.{Q(schema.OrderDateColumn)} AS OrderDateUtc,
                p.{Q(schema.Orders["TipoPedido"])} AS LegacyOrderType,
                COALESCE(
                    NULLIF(TRIM(c.{Q(schema.Customers["XRazonSocial"])}), ''),
                    NULLIF(TRIM(CONCAT_WS(' ', NULLIF(c.{Q(schema.Customers["Nombre"])}, ''), NULLIF(c.{Q(schema.Customers["Paterno"])}, ''), NULLIF(c.{Q(schema.Customers["Materno"])}, ''))), ''),
                    NULLIF(TRIM(c.{Q(schema.Customers["Cliente"])}), ''),
                    CONCAT('Cliente ', p.{Q(schema.Orders["noCliente"])})
                ) AS CustomerName,
                p.{Q(schema.Orders["MontoPedido"])} AS Total
            FROM {Q(schema.Orders.ActualName)} p
            INNER JOIN {Q(schema.Customers.ActualName)} c
                ON p.{Q(schema.Orders["noCliente"])} = c.{Q(schema.Customers["noCliente"])}
            WHERE p.{Q(schema.OrderDateColumn)} >= @fromDateUtc
              AND p.{Q(schema.OrderDateColumn)} < @toDateUtcExclusive
              AND (@customerQueryLike IS NULL OR UPPER({BuildCustomerNameExpression(schema)}) LIKE @customerQueryLike)
              AND p.{Q(schema.Orders["noCliente"])} <> 0
              AND p.{Q(schema.Orders["MontoPedido"])} <> 0.00
              AND p.{Q(schema.Orders["refPedido"])} IS NOT NULL
            ORDER BY p.{Q(schema.OrderDateColumn)} DESC, p.{Q(schema.Orders["noPedido"])} DESC
            LIMIT @skip, @take;
            """;
    }

    private static string BuildCustomerNameExpression(LegacyOrderReadSchema schema)
    {
        return $"""
            COALESCE(
                NULLIF(TRIM(c.{Q(schema.Customers["XRazonSocial"])}), ''),
                NULLIF(TRIM(CONCAT_WS(' ', NULLIF(c.{Q(schema.Customers["Nombre"])}, ''), NULLIF(c.{Q(schema.Customers["Paterno"])}, ''), NULLIF(c.{Q(schema.Customers["Materno"])}, ''))), ''),
                NULLIF(TRIM(c.{Q(schema.Customers["Cliente"])}), ''),
                CONCAT('Cliente ', p.{Q(schema.Orders["noCliente"])})
            )
            """;
    }

    private static async Task<int> CountAsync(
        MySqlConnection connection,
        string sql,
        LegacyOrderSearchReadModel search,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@fromDateUtc", search.FromDateUtc);
        command.Parameters.AddWithValue("@toDateUtcExclusive", search.ToDateUtcExclusive);
        command.Parameters.AddWithValue("@customerQueryLike", BuildLikeValue(search.CustomerQuery));

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    private static async Task<IReadOnlyList<LegacyOrderListItemReadModel>> ReadPageAsync(
        MySqlConnection connection,
        string sql,
        LegacyOrderSearchReadModel search,
        CancellationToken cancellationToken)
    {
        var items = new List<LegacyOrderListItemReadModel>();

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@fromDateUtc", search.FromDateUtc);
        command.Parameters.AddWithValue("@toDateUtcExclusive", search.ToDateUtcExclusive);
        command.Parameters.AddWithValue("@customerQueryLike", BuildLikeValue(search.CustomerQuery));
        command.Parameters.AddWithValue("@skip", (search.Page - 1) * search.PageSize);
        command.Parameters.AddWithValue("@take", search.PageSize);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new LegacyOrderListItemReadModel
            {
                LegacyOrderId = GetRequiredString(reader, "LegacyOrderId"),
                OrderDateUtc = GetRequiredDateTime(reader, "OrderDateUtc"),
                CustomerName = GetRequiredString(reader, "CustomerName"),
                Total = GetRequiredDecimal(reader, "Total"),
                LegacyOrderType = GetNullableString(reader, "LegacyOrderType")
            });
        }

        return items;
    }

    private static object BuildLikeValue(string? customerQuery)
    {
        if (string.IsNullOrWhiteSpace(customerQuery))
        {
            return DBNull.Value;
        }

        return $"%{customerQuery.Trim().ToUpperInvariant()}%";
    }

    private static string GetRequiredString(MySqlDataReader reader, string columnName)
    {
        var value = GetStringValue(reader, columnName);

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Column '{columnName}' returned null or empty, but a value was required.");
        }

        return value;
    }

    private static string? GetNullableString(MySqlDataReader reader, string columnName)
    {
        return GetStringValue(reader, columnName);
    }

    private static string? GetStringValue(MySqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        var value = reader.GetValue(ordinal);
        return Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    private static decimal GetRequiredDecimal(MySqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        if (reader.IsDBNull(ordinal))
        {
            throw new InvalidOperationException($"Column '{columnName}' returned null, but a numeric value was required.");
        }

        var value = reader.GetValue(ordinal);
        return Convert.ToDecimal(value, CultureInfo.InvariantCulture);
    }

    private static DateTime GetRequiredDateTime(MySqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        if (reader.IsDBNull(ordinal))
        {
            throw new InvalidOperationException($"Column '{columnName}' returned null, but a date/time value was required.");
        }

        var value = reader.GetValue(ordinal);
        return Convert.ToDateTime(value, CultureInfo.InvariantCulture);
    }

    internal static decimal NormalizeGrossAmountToNet(decimal grossAmount)
    {
        return Math.Round(grossAmount / 1.16m, 6, MidpointRounding.AwayFromZero);
    }

    internal static string BuildDescription(string? nominalArticleName, string? articleName, string? articleSpecification, string? fallbackArticleId = null)
    {
        if (!string.IsNullOrWhiteSpace(nominalArticleName))
        {
            return nominalArticleName.Trim();
        }

        var composed = string.Join(
            ' ',
            new[] { articleName, articleSpecification }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim()));

        return string.IsNullOrWhiteSpace(composed)
            ? fallbackArticleId?.Trim() ?? string.Empty
            : composed;
    }

    private static string Q(string identifier)
    {
        return $"`{identifier.Replace("`", "``", StringComparison.Ordinal)}`";
    }
}
