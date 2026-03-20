namespace Pineda.Facturacion.Application.Common;

internal static class FiscalMasterDataNormalization
{
    public static string NormalizeRequiredCode(string value)
    {
        return value.Trim().ToUpperInvariant();
    }

    public static string NormalizeRequiredText(string value)
    {
        return value.Trim();
    }

    public static string NormalizeSearchableText(string value)
    {
        return value.Trim().ToUpperInvariant();
    }

    public static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public static string NormalizeRfc(string value)
    {
        return NormalizeRequiredCode(value);
    }
}
