namespace Pineda.Facturacion.Application.Common;

public static class CfdiMonetaryRules
{
    public static int ResolveCurrencyScale(string? currencyCode)
    {
        return string.Equals(currencyCode, "MXN", StringComparison.OrdinalIgnoreCase) ? 2 : 2;
    }

    public static decimal RoundMonetary(decimal value, int currencyScale)
    {
        return Math.Round(value, currencyScale, MidpointRounding.AwayFromZero);
    }

    public static decimal RoundMonetary(decimal value, string? currencyCode)
    {
        return RoundMonetary(value, ResolveCurrencyScale(currencyCode));
    }

    public static bool AreEquivalentAtCurrencyScale(decimal left, decimal right, string? currencyCode)
    {
        var currencyScale = ResolveCurrencyScale(currencyCode);
        return RoundMonetary(left, currencyScale) == RoundMonetary(right, currencyScale);
    }

    public static decimal ResolveCurrencyTolerance(string? currencyCode)
    {
        var currencyScale = ResolveCurrencyScale(currencyCode);
        var tolerance = 1m;
        for (var index = 0; index < currencyScale; index++)
        {
            tolerance /= 10m;
        }

        return tolerance;
    }

    public static bool AreEquivalentWithinCurrencyTolerance(decimal left, decimal right, string? currencyCode)
    {
        var normalizedLeft = RoundMonetary(left, currencyCode);
        var normalizedRight = RoundMonetary(right, currencyCode);
        return Math.Abs(normalizedLeft - normalizedRight) <= ResolveCurrencyTolerance(currencyCode);
    }
}
