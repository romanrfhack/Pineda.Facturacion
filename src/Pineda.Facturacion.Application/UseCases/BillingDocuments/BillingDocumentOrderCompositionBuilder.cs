using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.BillingDocuments;

internal static class BillingDocumentOrderCompositionBuilder
{
    public static List<BillingDocumentItem> BuildBillingItems(IReadOnlyList<SalesOrder> salesOrders)
    {
        var items = new List<BillingDocumentItem>();
        var nextLineNumber = 1;

        foreach (var salesOrder in salesOrders)
        {
            foreach (var salesOrderItem in salesOrder.Items.OrderBy(x => x.LineNumber))
            {
                items.Add(new BillingDocumentItem
                {
                    LineNumber = nextLineNumber++,
                    Sku = salesOrderItem.Sku,
                    ProductInternalCode = string.IsNullOrWhiteSpace(salesOrderItem.Sku)
                        ? null
                        : FiscalMasterDataNormalization.NormalizeRequiredCode(salesOrderItem.Sku),
                    Description = salesOrderItem.Description,
                    Quantity = salesOrderItem.Quantity,
                    UnitPrice = salesOrderItem.UnitPrice,
                    DiscountAmount = salesOrderItem.DiscountAmount,
                    TaxRate = StandardVat16Calculator.StandardVatRate,
                    TaxAmount = 0m,
                    LineTotal = 0m,
                    SatProductServiceCode = salesOrderItem.SatProductServiceCode,
                    SatUnitCode = salesOrderItem.SatUnitCode,
                    TaxObjectCode = "02"
                });
            }
        }

        return items;
    }
}
