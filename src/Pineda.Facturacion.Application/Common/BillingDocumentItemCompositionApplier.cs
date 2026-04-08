using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.Common;

internal static class BillingDocumentItemCompositionApplier
{
    public static void Apply(
        BillingDocument billingDocument,
        IReadOnlyList<SalesOrder> salesOrders,
        IReadOnlyList<BillingDocumentItem> nextItems)
    {
        var currentItemsByKey = billingDocument.Items
            .ToDictionary(BillingDocumentItemFiscalSemanticKey.Build, StringComparer.Ordinal);
        var usedKeys = new HashSet<string>(StringComparer.Ordinal);
        var additions = new List<BillingDocumentItem>();

        foreach (var nextItem in nextItems)
        {
            var key = BillingDocumentItemFiscalSemanticKey.Build(nextItem);
            if (currentItemsByKey.TryGetValue(key, out var currentItem) && usedKeys.Add(key))
            {
                ApplyValues(currentItem, nextItem);
                continue;
            }

            additions.Add(nextItem);
        }

        var removals = billingDocument.Items
            .Where(currentItem => !usedKeys.Contains(BillingDocumentItemFiscalSemanticKey.Build(currentItem)))
            .ToList();

        foreach (var removal in removals)
        {
            billingDocument.Items.Remove(removal);
        }

        foreach (var addition in additions)
        {
            billingDocument.Items.Add(addition);
        }

        billingDocument.SalesOrderId = salesOrders[0].Id;
        StandardVat16Calculator.ApplyStandardVat(billingDocument);
        billingDocument.UpdatedAtUtc = DateTime.UtcNow;
    }

    private static void ApplyValues(BillingDocumentItem current, BillingDocumentItem next)
    {
        current.SalesOrderId = next.SalesOrderId;
        current.SalesOrderItemId = next.SalesOrderItemId;
        current.SourceBillingDocumentItemRemovalId = next.SourceBillingDocumentItemRemovalId;
        current.SourceSalesOrderLineNumber = next.SourceSalesOrderLineNumber;
        current.SourceLegacyOrderId = next.SourceLegacyOrderId;
        current.LineNumber = next.LineNumber;
        current.Sku = next.Sku;
        current.ProductInternalCode = next.ProductInternalCode;
        current.Description = next.Description;
        current.Quantity = next.Quantity;
        current.UnitPrice = next.UnitPrice;
        current.DiscountAmount = next.DiscountAmount;
        current.TaxRate = next.TaxRate;
        current.TaxAmount = next.TaxAmount;
        current.LineTotal = next.LineTotal;
        current.SatProductServiceCode = next.SatProductServiceCode;
        current.SatUnitCode = next.SatUnitCode;
        current.TaxObjectCode = next.TaxObjectCode;
    }
}
