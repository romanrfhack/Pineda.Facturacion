using System.Data;
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
            c.XRazonSocial AS CustomerName,
            c.RFC AS CustomerRfc,
            p.condPagoPedido AS PaymentCondition,
            c.TipoCliente AS PriceListCode,
            p.TipoEntrega AS DeliveryType,
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
            a.PorcentajeIVAArt AS TaxRate,
            a.IVA AS TaxAmount,
            (d.SuPrecio * d.Cantidad) AS LineTotal
        FROM pedidosdet d
        INNER JOIN articulos a
            ON d.cveArticulo = a.cveArticulo
           AND d.cveMarcaArticulo = a.cveMarcaArticulo
        WHERE d.noPedido = @legacyOrderId;
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
            LegacyOrderId = reader.GetString(reader.GetOrdinal("LegacyOrderId")),
            LegacyOrderNumber = reader.GetString(reader.GetOrdinal("LegacyOrderNumber")),
            LegacyOrderType = GetNullableString(reader, "LegacyOrderType"),
            CustomerLegacyId = reader.GetString(reader.GetOrdinal("CustomerLegacyId")),
            CustomerName = reader.GetString(reader.GetOrdinal("CustomerName")),
            CustomerRfc = GetNullableString(reader, "CustomerRfc"),
            PaymentCondition = reader.GetString(reader.GetOrdinal("PaymentCondition")),
            PriceListCode = GetNullableString(reader, "PriceListCode"),
            DeliveryType = GetNullableString(reader, "DeliveryType"),
            CurrencyCode = "MXN",
            // docs/013 maps subtotal to TBD, so this remains intentionally unmapped.
            // docs/013 maps discount total to TBD, so this remains intentionally unmapped.
            // docs/013 maps tax total to TBD, so this remains intentionally unmapped.
            Total = reader.GetDecimal(reader.GetOrdinal("Total"))
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

        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new LegacyOrderItemReadModel
            {
                // docs/013 still marks line number as TBD, so this remains intentionally unmapped.
                LegacyArticleId = reader.GetString(reader.GetOrdinal("LegacyArticleId")),
                // docs/013 documents Sku as a temporary same-source mapping from cveArticulo.
                Sku = GetNullableString(reader, "Sku"),
                Description = BuildDescription(
                    GetNullableString(reader, "ArticleName"),
                    GetNullableString(reader, "ArticleSpecification")),
                UnitCode = GetNullableString(reader, "UnitCode"),
                UnitName = GetNullableString(reader, "UnitName"),
                Quantity = reader.GetDecimal(reader.GetOrdinal("Quantity")),
                UnitPrice = reader.GetDecimal(reader.GetOrdinal("UnitPrice")),
                // docs/013 maps discount amount to TBD, so this remains intentionally unmapped.
                TaxRate = reader.GetDecimal(reader.GetOrdinal("TaxRate")),
                TaxAmount = reader.GetDecimal(reader.GetOrdinal("TaxAmount")),
                LineTotal = reader.GetDecimal(reader.GetOrdinal("LineTotal")),
                // docs/013 maps SAT product/service code to TBD, so this remains intentionally unmapped.
                // docs/013 maps SAT unit code to TBD, so this remains intentionally unmapped.
            });
        }

        return items;
    }

    private static string? GetNullableString(MySqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static string BuildDescription(string? articleName, string? articleSpecification)
    {
        return string.Join(
            ' ',
            new[] { articleName, articleSpecification }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
    }
}
