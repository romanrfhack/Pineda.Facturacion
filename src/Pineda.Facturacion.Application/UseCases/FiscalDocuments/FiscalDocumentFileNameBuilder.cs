using Pineda.Facturacion.Application.Common;

namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public static class FiscalDocumentFileNameBuilder
{
    public static string Build(
        string issuerRfc,
        string? series,
        string? folio,
        string receiverRfc,
        string fallbackToken,
        string extension)
    {
        var normalizedIssuerRfc = FiscalMasterDataNormalization.NormalizeRfc(issuerRfc);
        var normalizedReceiverRfc = FiscalMasterDataNormalization.NormalizeRfc(receiverRfc);
        var documentToken = string.Concat(
            FiscalMasterDataNormalization.NormalizeOptionalText(series) ?? string.Empty,
            FiscalMasterDataNormalization.NormalizeOptionalText(folio) ?? string.Empty);

        var middleToken = string.IsNullOrWhiteSpace(documentToken)
            ? fallbackToken.Trim()
            : documentToken;

        return $"{Sanitize(normalizedIssuerRfc)}_{Sanitize(middleToken)}_{Sanitize(normalizedReceiverRfc)}.{extension.TrimStart('.')}";
    }

    private static string Sanitize(string value)
    {
        return string.Join(
            string.Empty,
            value.Select(static character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
    }
}
