using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.ImportLegacyOrder;

internal static class LegacyOrderSnapshotMapper
{
    public static SalesOrder MapToSalesOrder(
        Models.Legacy.LegacyOrderReadModel legacyOrder,
        long legacyImportRecordId)
    {
        var salesOrder = new SalesOrder
        {
            LegacyImportRecordId = legacyImportRecordId,
            LegacyOrderNumber = legacyOrder.LegacyOrderNumber,
            LegacyOrderType = legacyOrder.LegacyOrderType,
            CustomerLegacyId = legacyOrder.CustomerLegacyId,
            CustomerName = legacyOrder.CustomerName,
            CustomerRfc = legacyOrder.CustomerRfc,
            PaymentCondition = legacyOrder.PaymentCondition,
            PriceListCode = legacyOrder.PriceListCode,
            DeliveryType = legacyOrder.DeliveryType,
            CurrencyCode = legacyOrder.CurrencyCode,
            SnapshotTakenAtUtc = DateTime.UtcNow,
            Status = SalesOrderStatus.SnapshotCreated,
            Items = legacyOrder.Items.Select(item => new SalesOrderItem
            {
                LineNumber = item.LineNumber,
                LegacyArticleId = item.LegacyArticleId,
                Sku = item.Sku,
                Description = item.Description,
                UnitCode = item.UnitCode,
                UnitName = item.UnitName,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                DiscountAmount = item.DiscountAmount,
                TaxRate = StandardVat16Calculator.StandardVatRate,
                TaxAmount = 0m,
                LineTotal = 0m,
                SatProductServiceCode = item.SatProductServiceCode,
                SatUnitCode = item.SatUnitCode
            }).ToList()
        };

        StandardVat16Calculator.ApplyStandardVat(salesOrder);
        return salesOrder;
    }
}
