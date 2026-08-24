namespace Pineda.Facturacion.Application.Common;

public sealed record LegacyPaymentSuggestion(
    string PaymentMethodSat,
    string PaymentFormSat,
    bool IsCreditSale,
    string SourceDescription);

public static class LegacyPaymentSuggestionPolicy
{
    public static LegacyPaymentSuggestion? Resolve(string? legacyPaymentCode)
    {
        return legacyPaymentCode?.Trim() switch
        {
            "10" => new LegacyPaymentSuggestion("PUE", "01", false, "Efectivo"),
            "20" => new LegacyPaymentSuggestion("PUE", "04", false, "TDC"),
            "30" => new LegacyPaymentSuggestion("PUE", "28", false, "TDD"),
            "40" => new LegacyPaymentSuggestion("PUE", "03", false, "Transferencia"),
            "50" => new LegacyPaymentSuggestion("PPD", "99", true, "Crédito"),
            _ => null
        };
    }

    public static LegacyPaymentSuggestion? ResolveCommon(IEnumerable<string?> legacyPaymentCodes)
    {
        var codes = legacyPaymentCodes.ToArray();
        if (codes.Length == 0)
        {
            return null;
        }

        var suggestions = codes.Select(Resolve).ToArray();
        if (suggestions.Any(x => x is null))
        {
            return null;
        }

        var first = suggestions[0]!;
        return suggestions.All(x =>
                string.Equals(x!.PaymentMethodSat, first.PaymentMethodSat, StringComparison.Ordinal)
                && string.Equals(x.PaymentFormSat, first.PaymentFormSat, StringComparison.Ordinal)
                && x.IsCreditSale == first.IsCreditSale)
            ? first
            : null;
    }
}
