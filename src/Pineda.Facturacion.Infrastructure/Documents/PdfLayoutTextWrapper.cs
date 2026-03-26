using System.Text;

namespace Pineda.Facturacion.Infrastructure.Documents;

internal static class PdfLayoutTextWrapper
{
    public static string FitTextToWidth(string value, float availableWidth, float fontSize, bool isBold = false)
    {
        var remaining = value?.TrimStart() ?? string.Empty;
        if (remaining.Length == 0)
        {
            return string.Empty;
        }

        var safeWidth = Math.Max(6f, availableWidth);
        var words = remaining.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return string.Empty;
        }

        var current = new StringBuilder();
        foreach (var word in words)
        {
            var candidate = current.Length == 0 ? word : $"{current} {word}";
            if (EstimateTextWidth(candidate, fontSize, isBold) <= safeWidth)
            {
                current.Clear();
                current.Append(candidate);
                continue;
            }

            if (current.Length > 0)
            {
                return current.ToString();
            }

            return FitTokenToWidth(word, safeWidth, fontSize, isBold);
        }

        return current.ToString();
    }

    private static string FitTokenToWidth(string token, float availableWidth, float fontSize, bool isBold)
    {
        var chunk = new StringBuilder();
        foreach (var ch in token)
        {
            var candidate = $"{chunk}{ch}";
            if (EstimateTextWidth(candidate, fontSize, isBold) > availableWidth && chunk.Length > 0)
            {
                return chunk.ToString();
            }

            chunk.Append(ch);
        }

        return chunk.Length == 0 ? token[..1] : chunk.ToString();
    }

    public static string[] WrapValueWithHangingLabel(string label, string value, float maxWidth, float labelFontSize, float valueFontSize, float labelValueGap = 4f, float rightSafetyPadding = 12f)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var labelText = $"{label}: ";
        var labelWidth = EstimateTextWidth(labelText, labelFontSize, isBold: true) + labelValueGap;
        var remaining = value.Trim();
        var lines = new List<string>();
        var isFirstLine = true;

        while (remaining.Length > 0)
        {
            var usableWidth = isFirstLine
                ? Math.Max(20f, maxWidth - labelWidth - rightSafetyPadding)
                : Math.Max(20f, maxWidth - rightSafetyPadding);

            var segment = FitTextToWidth(remaining, usableWidth, valueFontSize);
            if (string.IsNullOrEmpty(segment))
            {
                break;
            }

            lines.Add(segment);
            remaining = remaining[segment.Length..].TrimStart();
            isFirstLine = false;
        }

        return lines.ToArray();
    }

    private static float EstimateTextWidth(string text, float fontSize, bool isBold)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0f;
        }

        var multiplier = isBold ? 0.56f : 0.54f;
        return text.Normalize(NormalizationForm.FormKD).Length * fontSize * multiplier;
    }

    private static int EstimateWrapLength(float width, float fontSize, bool isBold)
    {
        var averageCharacterWidth = fontSize * (isBold ? 0.56f : 0.54f);
        return Math.Max(6, (int)Math.Floor(width / averageCharacterWidth));
    }
}
