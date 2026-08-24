using Pineda.Facturacion.Application.Common;

namespace Pineda.Facturacion.UnitTests;

public class LegacyPaymentSuggestionPolicyTests
{
    [Theory]
    [InlineData("10", "PUE", "01", false, "Efectivo")]
    [InlineData("20", "PUE", "04", false, "TDC")]
    [InlineData("30", "PUE", "28", false, "TDD")]
    [InlineData("40", "PUE", "03", false, "Transferencia")]
    [InlineData("50", "PPD", "99", true, "Crédito")]
    public void Resolve_Maps_Recognized_Legacy_Payment_Codes(
        string legacyCode,
        string expectedMethod,
        string expectedForm,
        bool expectedCredit,
        string expectedDescription)
    {
        var result = LegacyPaymentSuggestionPolicy.Resolve(legacyCode);

        Assert.NotNull(result);
        Assert.Equal(expectedMethod, result.PaymentMethodSat);
        Assert.Equal(expectedForm, result.PaymentFormSat);
        Assert.Equal(expectedCredit, result.IsCreditSale);
        Assert.Equal(expectedDescription, result.SourceDescription);
    }

    [Theory]
    [InlineData("4")]
    [InlineData("666")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Resolve_Does_Not_Guess_Unknown_Legacy_Codes(string? legacyCode)
    {
        Assert.Null(LegacyPaymentSuggestionPolicy.Resolve(legacyCode));
    }

    [Fact]
    public void ResolveCommon_Returns_Suggestion_When_All_Orders_Agree()
    {
        var result = LegacyPaymentSuggestionPolicy.ResolveCommon(["40", "40", "40"]);

        Assert.NotNull(result);
        Assert.Equal("PUE", result.PaymentMethodSat);
        Assert.Equal("03", result.PaymentFormSat);
        Assert.False(result.IsCreditSale);
    }

    [Fact]
    public void ResolveCommon_Returns_Null_When_Recognized_Orders_Disagree()
    {
        Assert.Null(LegacyPaymentSuggestionPolicy.ResolveCommon(["10", "30"]));
    }

    [Fact]
    public void ResolveCommon_Returns_Null_When_Any_Order_Is_Unknown()
    {
        Assert.Null(LegacyPaymentSuggestionPolicy.ResolveCommon(["40", "4"]));
    }
}
