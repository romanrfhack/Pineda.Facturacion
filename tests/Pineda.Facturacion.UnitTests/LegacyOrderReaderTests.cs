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
    public void CalculateDiscountAmountFromGrossPrices_Normalizes_Discount_To_Base_Amount()
    {
        var discountAmount = LegacyOrderReader.CalculateDiscountAmountFromGrossPrices(
            quantity: 2m,
            grossListUnitPrice: 116m,
            grossEffectiveUnitPrice: 87m);

        Assert.Equal(50m, discountAmount);
    }

    [Fact]
    public void DetailSql_Joins_NombresArticulos_And_Uses_Precio2()
    {
        var field = typeof(LegacyOrderReader).GetField("DetailSql", BindingFlags.Static | BindingFlags.NonPublic);
        var sql = Assert.IsType<string>(field?.GetValue(null));

        Assert.Contains("LEFT JOIN nombresarticulos n", sql, StringComparison.Ordinal);
        Assert.Contains("a.cveNomArt = n.cveNomArt", sql, StringComparison.Ordinal);
        Assert.Contains("n.NomArt AS NomArt", sql, StringComparison.Ordinal);
        Assert.Contains("a.Precio2 AS GrossListUnitPrice", sql, StringComparison.Ordinal);
    }
}
