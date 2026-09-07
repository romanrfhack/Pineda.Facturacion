using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.ImportLegacyOrder;

internal static class LegacyOrderSnapshotMapper
{
    internal const int MaxPersistedCustomerRfcLength = 13;

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
            CustomerRfc = NormalizeCustomerRfc(legacyOrder.CustomerRfc),
            PaymentCondition = legacyOrder.PaymentCondition,
            LegacyPaymentCode = legacyOrder.LegacyPaymentCode,
            LegacyPaymentDescription = legacyOrder.LegacyPaymentDescription,
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

    internal static string? NormalizeCustomerRfc(string? customerRfc)
    {
        if (string.IsNullOrWhiteSpace(customerRfc))
        {
            return null;
        }

        var normalized = customerRfc.Trim().ToUpperInvariant();

        // The commercial snapshot can only carry a Mexican RFC-sized value.
        // Overlength legacy values remain intact in the source hash and revision
        // SnapshotJson; the selected FiscalReceiver remains the fiscal source of truth.
        return normalized.Length <= MaxPersistedCustomerRfcLength
            ? normalized
            : null;
    }
}
