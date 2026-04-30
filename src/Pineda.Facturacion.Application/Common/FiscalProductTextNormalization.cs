using System.Globalization;
using System.Text;

namespace Pineda.Facturacion.Application.Common;

internal static class FiscalProductTextNormalization
{
    public static string? NormalizeOptionalKey(string? value)
    {
        var normalized = NormalizeOptionalText(value);
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    public static string? NormalizeOptionalText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var withoutAccents = RemoveDiacritics(value.Trim()).ToUpperInvariant();
        var builder = new StringBuilder(withoutAccents.Length);
        var previousWasSpace = false;

        foreach (var character in withoutAccents)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasSpace = false;
                continue;
            }

            if (char.IsWhiteSpace(character) || IsComparableSeparator(character))
            {
                if (!previousWasSpace && builder.Length > 0)
                {
                    builder.Append(' ');
                    previousWasSpace = true;
                }
            }
        }

        return builder.ToString().Trim();
    }

    public static string NormalizeHeader(string value)
    {
        return NormalizeOptionalText(value) ?? string.Empty;
    }

    private static bool IsComparableSeparator(char character)
    {
        return character is '-' or '_' or '.' or '/' or '\\' or ',' or ';' or ':' or '(' or ')' or '[' or ']' or '#';
    }

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
