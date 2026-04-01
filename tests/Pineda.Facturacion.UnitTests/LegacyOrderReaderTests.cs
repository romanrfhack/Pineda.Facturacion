using Pineda.Facturacion.Infrastructure.LegacyRead.Readers;

namespace Pineda.Facturacion.UnitTests;

public class LegacyOrderReaderTests
{
    [Fact]
    public void BuildDescription_Prefers_NomArt_From_NombresArticulos()
    {
        var description = LegacyOrderReader.BuildDescription("Nombre correcto", "Articulo legacy", "Especificacion", "A-1");

        Assert.Equal("Nombre correcto", description);
    }

    [Fact]
    public void NormalizeGrossAmountToNet_Converts_Precio2_With_Standard_Vat()
    {
        var unitPrice = LegacyOrderReader.NormalizeGrossAmountToNet(116m);

        Assert.Equal(100m, unitPrice);
    }

    [Fact]
    public void NormalizeGrossAmountToNet_Converts_SuPrecio_With_Standard_Vat()
    {
        var unitPrice = LegacyOrderReader.NormalizeGrossAmountToNet(87m);

        Assert.Equal(75m, unitPrice);
    }

    [Fact]
    public void LegacySchemaResolver_Resolves_Table_Name_Case_Insensitively()
    {
        var resolved = LegacySchemaResolver.SelectResolvedTableName(
            "legacydb",
            "pedidos",
            ["Pedidos"],
            ["Pedidos", "Clientes"]);

        Assert.Equal("Pedidos", resolved);
    }

    [Fact]
    public void BuildDetailSql_Uses_Resolved_Table_Names_And_Effective_Price()
    {
        var schema = CreateResolvedSchema(
            ordersTableName: "Pedidos",
            customersTableName: "Clientes",
            orderItemsTableName: "PedidosDet",
            articlesTableName: "Articulos",
            articleNamesTableName: "NombresArticulos",
            orderDateColumnName: "FechaPedido");

        var sql = LegacyOrderReader.BuildDetailSql(schema);

        Assert.Contains("FROM `PedidosDet` d", sql, StringComparison.Ordinal);
        Assert.Contains("INNER JOIN `Articulos` a", sql, StringComparison.Ordinal);
        Assert.Contains("LEFT JOIN `NombresArticulos` n", sql, StringComparison.Ordinal);
        Assert.Contains("n.`NomArt` AS NomArt", sql, StringComparison.Ordinal);
        Assert.Contains("d.`SuPrecio` AS GrossEffectiveUnitPrice", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("FROM pedidosdet", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildDetailSql_Joins_NombresArticulos_On_NomArt_Categoria_And_Grupo()
    {
        var schema = CreateResolvedSchema(
            ordersTableName: "Pedidos",
            customersTableName: "Clientes",
            orderItemsTableName: "PedidosDet",
            articlesTableName: "Articulos",
            articleNamesTableName: "NombresArticulos",
            orderDateColumnName: "FechaPedido");

        var sql = LegacyOrderReader.BuildDetailSql(schema);

        Assert.Contains("ON a.`cveNomArt` = n.`cveNomArt`", sql, StringComparison.Ordinal);
        Assert.Contains("AND a.`cveCategoria` = n.`cveCategoria`", sql, StringComparison.Ordinal);
        Assert.Contains("AND a.`cveGrupo` = n.`cveGrupo`", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildHeaderSql_Uses_Resolved_Table_Name_For_Pedidos()
    {
        var schema = CreateResolvedSchema(
            ordersTableName: "Pedidos",
            customersTableName: "Clientes",
            orderItemsTableName: "PedidosDet",
            articlesTableName: "Articulos",
            articleNamesTableName: "NombresArticulos",
            orderDateColumnName: "FechaPedido");

        var sql = LegacyOrderReader.BuildHeaderSql(schema);

        Assert.Contains("FROM `Pedidos` p", sql, StringComparison.Ordinal);
        Assert.Contains("INNER JOIN `Clientes` c", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("FROM pedidos p", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildListSql_Applies_Customer_Filter_Before_Pagination()
    {
        var schema = CreateResolvedSchema(
            ordersTableName: "Pedidos",
            customersTableName: "Clientes",
            orderItemsTableName: "PedidosDet",
            articlesTableName: "Articulos",
            articleNamesTableName: "NombresArticulos",
            orderDateColumnName: "FechaPedido");

        var sql = LegacyOrderReader.BuildListSql(schema);

        Assert.Contains("UPPER(COALESCE(", sql, StringComparison.Ordinal);
        Assert.Contains("LIKE @customerQueryLike", sql, StringComparison.Ordinal);
        Assert.Contains("ORDER BY p.`FechaPedido` DESC", sql, StringComparison.Ordinal);
        Assert.Contains("LIMIT @skip, @take", sql, StringComparison.Ordinal);
        Assert.True(sql.IndexOf("LIKE @customerQueryLike", StringComparison.Ordinal) < sql.IndexOf("LIMIT @skip, @take", StringComparison.Ordinal));
    }

    [Fact]
    public void LegacySchemaResolver_Column_Error_Includes_Diagnostics()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => LegacySchemaResolver.SelectResolvedColumnName(
            "legacydb",
            "pedidos",
            "Pedidos",
            ["Fecha", "FechaPedido"],
            ["noPedido", "MontoPedido"]));

        Assert.Contains("schema 'legacydb'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Logical table 'pedidos'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("physical table 'Pedidos'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("'Fecha'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("'FechaPedido'", exception.Message, StringComparison.Ordinal);
    }

    private static LegacyOrderReadSchema CreateResolvedSchema(
        string ordersTableName,
        string customersTableName,
        string orderItemsTableName,
        string articlesTableName,
        string articleNamesTableName,
        string orderDateColumnName)
    {
        return new LegacyOrderReadSchema(
            CreateTable(
                "pedidos",
                ordersTableName,
                ("noPedido", "noPedido"),
                ("refPedido", "refPedido"),
                ("TipoPedido", "TipoPedido"),
                ("noCliente", "noCliente"),
                ("condPagoPedido", "condPagoPedido"),
                ("TipoEntrega", "TipoEntrega"),
                ("MontoPedido", "MontoPedido")),
            CreateTable(
                "clientes",
                customersTableName,
                ("XRazonSocial", "XRazonSocial"),
                ("Nombre", "Nombre"),
                ("Paterno", "Paterno"),
                ("Materno", "Materno"),
                ("Cliente", "Cliente"),
                ("RFC", "RFC"),
                ("TipoCliente", "TipoCliente"),
                ("noCliente", "noCliente")),
            CreateTable(
                "pedidosdet",
                orderItemsTableName,
                ("SuPrecio", "SuPrecio"),
                ("Cantidad", "Cantidad"),
                ("noPedido", "noPedido"),
                ("cveArticulo", "cveArticulo"),
                ("cveMarcaArticulo", "cveMarcaArticulo"),
                ("uniMedida", "uniMedida")),
            CreateTable(
                "articulos",
                articlesTableName,
                ("cveArticulo", "cveArticulo"),
                ("cveMarcaArticulo", "cveMarcaArticulo"),
                ("Articulo", "Articulo"),
                ("Especificacion", "Especificacion"),
                ("uniMedida", "uniMedida"),
                ("cveNomArt", "cveNomArt"),
                ("cveCategoria", "cveCategoria"),
                ("cveGrupo", "cveGrupo")),
            CreateTable(
                "nombresarticulos",
                articleNamesTableName,
                ("NomArt", "NomArt"),
                ("cveNomArt", "cveNomArt"),
                ("cveCategoria", "cveCategoria"),
                ("cveGrupo", "cveGrupo")),
            orderDateColumnName);
    }

    private static ResolvedLegacyTable CreateTable(string logicalName, string actualName, params (string Logical, string Actual)[] columns)
    {
        return new ResolvedLegacyTable(
            logicalName,
            actualName,
            columns.ToDictionary(x => x.Logical, x => x.Actual, StringComparer.OrdinalIgnoreCase));
    }
}
