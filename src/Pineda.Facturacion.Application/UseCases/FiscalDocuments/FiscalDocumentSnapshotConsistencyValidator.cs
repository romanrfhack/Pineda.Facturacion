using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

internal static class FiscalDocumentSnapshotConsistencyValidator
{
    public static string? Validate(FiscalDocument fiscalDocument)
    {
        if (fiscalDocument.Items.Count == 0)
        {
            return "Fiscal document snapshot does not contain any persisted line items.";
        }

        var currencyCode = fiscalDocument.CurrencyCode;
        var subtotal = fiscalDocument.Items.Sum(x => x.Subtotal);
        var discountTotal = fiscalDocument.Items.Sum(x => x.DiscountAmount);
        var taxTotal = fiscalDocument.Items.Sum(x => x.TaxTotal);
        var total = fiscalDocument.Items.Sum(x => x.Total);

        if (!Matches(fiscalDocument.Subtotal, subtotal, currencyCode))
        {
            return $"Fiscal document subtotal '{fiscalDocument.Subtotal:0.######}' does not match the sum of line subtotals '{subtotal:0.######}'.";
        }

        if (!Matches(fiscalDocument.DiscountTotal, discountTotal, currencyCode))
        {
            return $"Fiscal document discount total '{fiscalDocument.DiscountTotal:0.######}' does not match the sum of line discounts '{discountTotal:0.######}'.";
        }

        if (!Matches(fiscalDocument.TaxTotal, taxTotal, currencyCode))
        {
            return $"Fiscal document tax total '{fiscalDocument.TaxTotal:0.######}' does not match the sum of line taxes '{taxTotal:0.######}'.";
        }

        if (!Matches(fiscalDocument.Total, total, currencyCode))
        {
            return $"Fiscal document total '{fiscalDocument.Total:0.######}' does not match the sum of line totals '{total:0.######}'.";
        }

        foreach (var item in fiscalDocument.Items.OrderBy(x => x.LineNumber))
        {
            if (item.Quantity < 0m)
            {
                return $"Fiscal document item line '{item.LineNumber}' contains a negative quantity '{item.Quantity:0.######}'.";
            }

            if (item.Subtotal < 0m || item.TaxTotal < 0m || item.Total < 0m)
            {
                return $"Fiscal document item line '{item.LineNumber}' contains negative monetary values.";
            }

            if (!Matches(item.Total, item.Subtotal + item.TaxTotal, currencyCode))
            {
                return $"Fiscal document item line '{item.LineNumber}' total '{item.Total:0.######}' does not match subtotal '{item.Subtotal:0.######}' plus tax '{item.TaxTotal:0.######}'.";
            }

            var grossAmount = item.Quantity * item.UnitPrice;
            var discountConsumesWholeGross = grossAmount > 0m && Matches(item.DiscountAmount, grossAmount, currencyCode);
            if (item.Quantity > 0m
                && grossAmount > 0m
                && item.Subtotal <= 0m
                && !discountConsumesWholeGross)
            {
                return $"Fiscal document item line '{item.LineNumber}' has positive quantity '{item.Quantity:0.######}' but persisted subtotal '{item.Subtotal:0.######}'.";
            }
        }

        return null;
    }

    private static bool Matches(decimal left, decimal right, string? currencyCode)
    {
        return CfdiMonetaryRules.AreEquivalentAtCurrencyScale(left, right, currencyCode);
    }
}
