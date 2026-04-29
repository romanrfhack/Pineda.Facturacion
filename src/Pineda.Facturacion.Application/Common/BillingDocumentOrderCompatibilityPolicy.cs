namespace Pineda.Facturacion.Application.Common;

internal static class BillingDocumentOrderCompatibilityPolicy
{
    public const string DifferentCustomerErrorCode = "DifferentCustomer";
    public const string DifferentCustomerRfcErrorCode = "DifferentCustomerRfc";
    public const string DifferentPaymentConditionErrorCode = "DifferentPaymentCondition";
    public const string DifferentCurrencyErrorCode = "DifferentCurrency";

    public static BillingDocumentOrderCompatibilityIssue? GetIssue(
        BillingDocumentOrderCompatibilitySnapshot baseline,
        BillingDocumentOrderCompatibilitySnapshot candidate)
    {
        if (!string.Equals(
                FiscalMasterDataNormalization.NormalizeRequiredText(baseline.CustomerLegacyId),
                FiscalMasterDataNormalization.NormalizeRequiredText(candidate.CustomerLegacyId),
                StringComparison.Ordinal))
        {
            return new BillingDocumentOrderCompatibilityIssue(
                DifferentCustomerErrorCode,
                $"{candidate.ReferenceLabel} belongs to a different customer than {baseline.ReferenceLabel}.");
        }

        var normalizedBaselineRfc = NormalizeOptionalRfc(baseline.CustomerRfc);
        var normalizedCandidateRfc = NormalizeOptionalRfc(candidate.CustomerRfc);
        if (normalizedBaselineRfc is not null
            && normalizedCandidateRfc is not null
            && !string.Equals(normalizedBaselineRfc, normalizedCandidateRfc, StringComparison.Ordinal))
        {
            return new BillingDocumentOrderCompatibilityIssue(
                DifferentCustomerRfcErrorCode,
                $"{candidate.ReferenceLabel} has RFC '{normalizedCandidateRfc}' which does not match RFC '{normalizedBaselineRfc}' from {baseline.ReferenceLabel}.");
        }

        var normalizedBaselinePaymentCondition = FiscalMasterDataNormalization.NormalizeOptionalText(baseline.PaymentCondition);
        var normalizedCandidatePaymentCondition = FiscalMasterDataNormalization.NormalizeOptionalText(candidate.PaymentCondition);
        if (!string.Equals(normalizedBaselinePaymentCondition, normalizedCandidatePaymentCondition, StringComparison.Ordinal))
        {
            return new BillingDocumentOrderCompatibilityIssue(
                DifferentPaymentConditionErrorCode,
                $"{candidate.ReferenceLabel} payment condition '{candidate.PaymentCondition}' does not match payment condition '{baseline.PaymentCondition}' from {baseline.ReferenceLabel}.");
        }

        var normalizedBaselineCurrency = FiscalMasterDataNormalization.NormalizeRequiredCode(baseline.CurrencyCode);
        var normalizedCandidateCurrency = FiscalMasterDataNormalization.NormalizeRequiredCode(candidate.CurrencyCode);
        if (!string.Equals(normalizedBaselineCurrency, normalizedCandidateCurrency, StringComparison.Ordinal))
        {
            return new BillingDocumentOrderCompatibilityIssue(
                DifferentCurrencyErrorCode,
                $"{candidate.ReferenceLabel} currency '{normalizedCandidateCurrency}' does not match currency '{normalizedBaselineCurrency}' from {baseline.ReferenceLabel}.");
        }

        return null;
    }

    public static BillingDocumentOrderCompatibilitySnapshot FromSalesOrder(
        long salesOrderId,
        string customerLegacyId,
        string customerName,
        string? customerRfc,
        string paymentCondition,
        string currencyCode)
    {
        return new BillingDocumentOrderCompatibilitySnapshot(
            $"Sales order '{salesOrderId}'",
            customerLegacyId,
            customerName,
            customerRfc,
            paymentCondition,
            currencyCode);
    }

    public static BillingDocumentOrderCompatibilitySnapshot FromLegacyOrder(
        string legacyOrderId,
        string customerLegacyId,
        string customerName,
        string? customerRfc,
        string paymentCondition,
        string currencyCode)
    {
        return new BillingDocumentOrderCompatibilitySnapshot(
            $"Legacy order '{legacyOrderId}'",
            customerLegacyId,
            customerName,
            customerRfc,
            paymentCondition,
            currencyCode);
    }

    private static string? NormalizeOptionalRfc(string? value)
    {
        var normalized = FiscalMasterDataNormalization.NormalizeOptionalText(value);
        return normalized is null ? null : FiscalMasterDataNormalization.NormalizeRfc(normalized);
    }
}

internal sealed record BillingDocumentOrderCompatibilitySnapshot(
    string ReferenceLabel,
    string CustomerLegacyId,
    string CustomerName,
    string? CustomerRfc,
    string PaymentCondition,
    string CurrencyCode);

internal sealed record BillingDocumentOrderCompatibilityIssue(
    string ErrorCode,
    string ErrorMessage);
