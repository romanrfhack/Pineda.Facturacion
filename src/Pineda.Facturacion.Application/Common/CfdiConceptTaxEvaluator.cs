namespace Pineda.Facturacion.Application.Common;

public static class CfdiConceptTaxEvaluator
{
    public static CfdiConceptTaxEvaluation Evaluate(
        string taxObjectCode,
        decimal subtotal,
        decimal taxTotal,
        int currencyScale)
    {
        var normalizedTaxObjectCode = string.IsNullOrWhiteSpace(taxObjectCode)
            ? string.Empty
            : taxObjectCode.Trim().ToUpperInvariant();

        if (!string.Equals(normalizedTaxObjectCode, "02", StringComparison.Ordinal))
        {
            return new CfdiConceptTaxEvaluation(
                EffectiveTaxObjectCode: normalizedTaxObjectCode,
                ShouldIncludeTraslado: false,
                ReportableBase: 0m,
                ReportableTaxAmount: 0m,
                ValidationError: null);
        }

        if (subtotal < 0m)
        {
            return new CfdiConceptTaxEvaluation(
                EffectiveTaxObjectCode: normalizedTaxObjectCode,
                ShouldIncludeTraslado: false,
                ReportableBase: 0m,
                ReportableTaxAmount: 0m,
                ValidationError: $"is taxable with tax object code '02' but has a negative subtotal '{subtotal}'.");
        }

        if (taxTotal < 0m)
        {
            return new CfdiConceptTaxEvaluation(
                EffectiveTaxObjectCode: normalizedTaxObjectCode,
                ShouldIncludeTraslado: false,
                ReportableBase: 0m,
                ReportableTaxAmount: 0m,
                ValidationError: $"is taxable with tax object code '02' but has a negative tax total '{taxTotal}'.");
        }

        if (subtotal <= 0m && taxTotal > 0m)
        {
            return new CfdiConceptTaxEvaluation(
                EffectiveTaxObjectCode: normalizedTaxObjectCode,
                ShouldIncludeTraslado: false,
                ReportableBase: 0m,
                ReportableTaxAmount: 0m,
                ValidationError: $"is taxable with tax object code '02' but has subtotal '{subtotal}' and tax total '{taxTotal}'. Review quantity, unit price, discount and stored tax totals before stamping.");
        }

        var reportableBase = CfdiMonetaryRules.RoundMonetary(subtotal, currencyScale);
        var reportableTaxAmount = CfdiMonetaryRules.RoundMonetary(taxTotal, currencyScale);

        if (reportableBase <= 0m)
        {
            return new CfdiConceptTaxEvaluation(
                EffectiveTaxObjectCode: "04",
                ShouldIncludeTraslado: false,
                ReportableBase: reportableBase,
                ReportableTaxAmount: reportableTaxAmount,
                ValidationError: null);
        }

        return new CfdiConceptTaxEvaluation(
            EffectiveTaxObjectCode: normalizedTaxObjectCode,
            ShouldIncludeTraslado: true,
            ReportableBase: reportableBase,
            ReportableTaxAmount: reportableTaxAmount,
            ValidationError: null);
    }
}

public readonly record struct CfdiConceptTaxEvaluation(
    string EffectiveTaxObjectCode,
    bool ShouldIncludeTraslado,
    decimal ReportableBase,
    decimal ReportableTaxAmount,
    string? ValidationError);
