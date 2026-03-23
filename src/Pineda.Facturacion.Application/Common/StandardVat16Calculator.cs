using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.Common;

public static class StandardVat16Calculator
{
    public const decimal StandardVatRate = 0.16m;

    public static CommercialAmounts CalculateLine(decimal quantity, decimal unitPrice, decimal discountAmount, decimal taxRate = StandardVatRate)
    {
        var grossSubtotal = Round(unitPrice * quantity);
        var normalizedDiscount = Round(discountAmount);
        var netSubtotal = Round(grossSubtotal - normalizedDiscount);
        var taxAmount = Round(netSubtotal * taxRate);
        var total = Round(netSubtotal + taxAmount);

        return new CommercialAmounts(
            TaxRate: Round(taxRate),
            Subtotal: netSubtotal,
            DiscountAmount: normalizedDiscount,
            TaxAmount: taxAmount,
            Total: total);
    }

    public static void ApplyStandardVat(SalesOrder salesOrder)
    {
        foreach (var item in salesOrder.Items)
        {
            ApplyStandardVat(item);
        }

        RecalculateTotals(salesOrder);
    }

    public static void ApplyStandardVat(SalesOrderItem item)
    {
        var amounts = CalculateLine(item.Quantity, item.UnitPrice, item.DiscountAmount);
        item.TaxRate = amounts.TaxRate;
        item.DiscountAmount = amounts.DiscountAmount;
        item.TaxAmount = amounts.TaxAmount;
        item.LineTotal = amounts.Subtotal;
    }

    public static void ApplyStandardVat(BillingDocument billingDocument)
    {
        foreach (var item in billingDocument.Items)
        {
            ApplyStandardVat(item);
        }

        RecalculateTotals(billingDocument);
    }

    public static void ApplyStandardVat(BillingDocumentItem item)
    {
        var amounts = CalculateLine(item.Quantity, item.UnitPrice, item.DiscountAmount);
        item.TaxRate = amounts.TaxRate;
        item.DiscountAmount = amounts.DiscountAmount;
        item.TaxAmount = amounts.TaxAmount;
        item.LineTotal = amounts.Subtotal;
    }

    public static void RecalculateTotals(SalesOrder salesOrder)
    {
        salesOrder.Subtotal = Round(salesOrder.Items.Sum(x => x.LineTotal));
        salesOrder.DiscountTotal = Round(salesOrder.Items.Sum(x => x.DiscountAmount));
        salesOrder.TaxTotal = Round(salesOrder.Items.Sum(x => x.TaxAmount));
        salesOrder.Total = Round(salesOrder.Subtotal + salesOrder.TaxTotal);
    }

    public static void RecalculateTotals(BillingDocument billingDocument)
    {
        billingDocument.Subtotal = Round(billingDocument.Items.Sum(x => x.LineTotal));
        billingDocument.DiscountTotal = Round(billingDocument.Items.Sum(x => x.DiscountAmount));
        billingDocument.TaxTotal = Round(billingDocument.Items.Sum(x => x.TaxAmount));
        billingDocument.Total = Round(billingDocument.Subtotal + billingDocument.TaxTotal);
    }

    private static decimal Round(decimal value)
    {
        return Math.Round(value, 6, MidpointRounding.AwayFromZero);
    }
}

public readonly record struct CommercialAmounts(
    decimal TaxRate,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal Total);
