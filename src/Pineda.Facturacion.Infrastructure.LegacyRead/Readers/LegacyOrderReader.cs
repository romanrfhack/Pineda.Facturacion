using System.Data;
using System.Globalization;
using MySqlConnector;
using Pineda.Facturacion.Application.Abstractions.Legacy;
using Pineda.Facturacion.Application.Models.Legacy;
using Pineda.Facturacion.Infrastructure.LegacyRead.Options;

namespace Pineda.Facturacion.Infrastructure.LegacyRead.Readers;

public class LegacyOrderReader : ILegacyOrderReader
{
    private const string HeaderSql = """
        SELECT
            p.noPedido AS LegacyOrderId,
            p.refPedido AS LegacyOrderNumber,
            p.TipoPedido AS LegacyOrderType,
            p.noCliente AS CustomerLegacyId,
            COALESCE(
                NULLIF(TRIM(c.XRazonSocial), ''),
                NULLIF(TRIM(CONCAT_WS(' ', NULLIF(c.Nombre, ''), NULLIF(c.Paterno, ''), NULLIF(c.Materno, ''))), ''),
                NULLIF(TRIM(c.Cliente), ''),
                CONCAT('Cliente ', p.noCliente)
            ) AS CustomerName,
            c.RFC AS CustomerRfc,
            p.condPagoPedido AS PaymentCondition,
            c.TipoCliente AS PriceListCode,
            p.TipoEntrega AS DeliveryType,
            COALESCE((
                SELECT SUM(d.SuPrecio * d.Cantidad)
                FROM pedidosdet d
                WHERE d.noPedido = p.noPedido
            ), 0) AS Subtotal,
            COALESCE((
                SELECT SUM((d.Precio - d.SuPrecio) * d.Cantidad)
                FROM pedidosdet d
                WHERE d.noPedido = p.noPedido
            ), 0) AS DiscountTotal,
            0 AS TaxTotal,
            p.MontoPedido AS Total
        FROM pedidos p
        INNER JOIN clientes c
            ON p.noCliente = c.noCliente
        WHERE p.noPedido = @legacyOrderId
          AND p.noCliente <> 0
          AND p.MontoPedido <> 0.00
          AND p.refPedido IS NOT NULL
        LIMIT 1;
        """;

    private const string DetailSql = """
        SELECT
            d.cveArticulo AS LegacyArticleId,
            d.cveArticulo AS Sku,
            a.Articulo AS ArticleName,
            a.Especificacion AS ArticleSpecification,
            d.uniMedida AS UnitCode,
            a.uniMedida AS UnitName,
            d.Cantidad AS Quantity,
            d.SuPrecio AS UnitPrice,
            ((d.Precio - d.SuPrecio) * d.Cantidad) AS DiscountAmount,
            0 AS TaxRate,
            0 AS TaxAmount,
            (d.SuPrecio * d.Cantidad) AS LineTotal
        FROM pedidosdet d
        INNER JOIN articulos a
            ON d.cveArticulo = a.cveArticulo
           AND d.cveMarcaArticulo = a.cveMarcaArticulo
        WHERE d.noPedido = @legacyOrderId
        ORDER BY d.cveArticulo, d.cveMarcaArticulo, d.SuPrecio, d.Cantidad, d.uniMedida;
        """;

    private readonly string _connectionString;

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

        var header = await ReadHeaderAsync(connection, legacyOrderId, cancellationToken);

        if (header is null)
        {
            return null;
        }

        header.Items = await ReadItemsAsync(connection, legacyOrderId, cancellationToken);
        return header;
    }

    private static async Task<LegacyOrderReadModel?> ReadHeaderAsync(
        MySqlConnection connection,
        string legacyOrderId,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(HeaderSql, connection);
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
        string legacyOrderId,
        CancellationToken cancellationToken)
    {
        var items = new List<LegacyOrderItemReadModel>();

        await using var command = new MySqlCommand(DetailSql, connection);
        command.Parameters.AddWithValue("@legacyOrderId", legacyOrderId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var lineNumber = 1;

        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new LegacyOrderItemReadModel
            {
                LineNumber = lineNumber++,
                LegacyArticleId = GetRequiredString(reader, "LegacyArticleId"),
                // docs/013 documents Sku as a temporary same-source mapping from cveArticulo.
                Sku = GetNullableString(reader, "Sku"),
                Description = BuildDescription(
                    GetNullableString(reader, "ArticleName"),
                    GetNullableString(reader, "ArticleSpecification")),
                UnitCode = GetNullableString(reader, "UnitCode"),
                UnitName = GetNullableString(reader, "UnitName"),
                Quantity = GetRequiredDecimal(reader, "Quantity"),
                UnitPrice = GetRequiredDecimal(reader, "UnitPrice"),
                DiscountAmount = GetRequiredDecimal(reader, "DiscountAmount"),
                TaxRate = GetRequiredDecimal(reader, "TaxRate"),
                TaxAmount = GetRequiredDecimal(reader, "TaxAmount"),
                LineTotal = GetRequiredDecimal(reader, "LineTotal"),
                // docs/013 maps SAT product/service code to TBD, so this remains intentionally unmapped.
                // docs/013 maps SAT unit code to TBD, so this remains intentionally unmapped.
            });
        }

        return items;
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

    private static string BuildDescription(string? articleName, string? articleSpecification)
    {
        return string.Join(
            ' ',
            new[] { articleName, articleSpecification }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
    }
}
