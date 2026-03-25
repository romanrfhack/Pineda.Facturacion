using System.Reflection;
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
    public void DetailSql_Joins_NombresArticulos_And_Uses_SuPrecio_As_Effective_Price()
    {
        var field = typeof(LegacyOrderReader).GetField("DetailSql", BindingFlags.Static | BindingFlags.NonPublic);
        var sql = Assert.IsType<string>(field?.GetValue(null));

        Assert.Contains("LEFT JOIN nombresarticulos n", sql, StringComparison.Ordinal);
        Assert.Contains("a.cveNomArt = n.cveNomArt", sql, StringComparison.Ordinal);
        Assert.Contains("n.NomArt AS NomArt", sql, StringComparison.Ordinal);
        Assert.Contains("d.SuPrecio AS GrossEffectiveUnitPrice", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("GrossListUnitPrice", sql, StringComparison.Ordinal);
    }
}
