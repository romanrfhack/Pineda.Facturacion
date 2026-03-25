namespace Pineda.Facturacion.Infrastructure.Documents;

internal static class PdfLayoutTextWrapper
{
    public static string[] WrapValueWithHangingLabel(string label, string value, float maxWidth, float labelFontSize, float valueFontSize)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var labelText = $"{label}: ";
        var labelWidth = EstimateTextWidth(labelText, labelFontSize, isBold: true) + 2f;
        var firstLineLength = EstimateWrapLength(Math.Max(20f, maxWidth - labelWidth), valueFontSize, isBold: false);
        var followingLineLength = EstimateWrapLength(maxWidth, valueFontSize, isBold: false);

        var remaining = value.Trim();
        if (remaining.Length == 0)
        {
            return [];
        }

        var lines = new List<string>();
        var firstLine = WrapText(remaining, firstLineLength).First();
        lines.Add(firstLine);

        remaining = remaining[firstLine.Length..].TrimStart();
        if (remaining.Length > 0)
        {
            lines.AddRange(WrapText(remaining, followingLineLength));
        }

        return lines.ToArray();
    }

    private static IEnumerable<string> WrapText(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var lines = new List<string>();
        var remaining = value.Trim();
        while (remaining.Length > maxLength)
        {
            var splitIndex = remaining.LastIndexOf(' ', maxLength);
            if (splitIndex <= 0)
            {
                splitIndex = maxLength;
            }

            lines.Add(remaining[..splitIndex].TrimEnd());
            remaining = remaining[splitIndex..].TrimStart();
        }

        lines.Add(remaining);
        return lines;
    }

    private static float EstimateTextWidth(string text, float fontSize, bool isBold)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0f;
        }

        var multiplier = isBold ? 0.56f : 0.52f;
        return text.Length * fontSize * multiplier;
    }

    private static int EstimateWrapLength(float width, float fontSize, bool isBold)
    {
        var averageCharacterWidth = fontSize * (isBold ? 0.56f : 0.52f);
        return Math.Max(6, (int)Math.Floor(width / averageCharacterWidth));
    }
}
