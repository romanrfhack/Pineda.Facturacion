namespace Pineda.Facturacion.Infrastructure.FacturaloPlus.FacturaloPlus;

internal static class FacturaloPlusOperationUri
{
    public static Uri BuildRelative(string baseUrl, string relativeOperationPath)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("FacturaloPlus base URL is required.");
        }

        if (string.IsNullOrWhiteSpace(relativeOperationPath))
        {
            throw new InvalidOperationException("FacturaloPlus operation path is required.");
        }

        var normalizedBaseUrl = NormalizeBaseUrl(baseUrl);
        var normalizedRelativePath = relativeOperationPath.Trim().TrimStart('/');

        return new Uri(new Uri(normalizedBaseUrl, UriKind.Absolute), normalizedRelativePath);
    }

    public static string NormalizeBaseUrl(string baseUrl)
    {
        var normalized = baseUrl.Trim();
        return normalized.EndsWith("/", StringComparison.Ordinal)
            ? normalized
            : normalized + "/";
    }
}
