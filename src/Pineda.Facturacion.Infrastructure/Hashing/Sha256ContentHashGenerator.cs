using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Pineda.Facturacion.Application.Abstractions.Hashing;
using Pineda.Facturacion.Application.Models.Legacy;

namespace Pineda.Facturacion.Infrastructure.Hashing;

public class Sha256ContentHashGenerator : IContentHashGenerator
{
    public string GenerateHash(LegacyOrderReadModel legacyOrder)
    {
        ArgumentNullException.ThrowIfNull(legacyOrder);

        var builder = new StringBuilder();

        Append(builder, "legacy_order_id", legacyOrder.LegacyOrderId);
        Append(builder, "legacy_order_number", legacyOrder.LegacyOrderNumber);
        Append(builder, "legacy_order_type", legacyOrder.LegacyOrderType);
        Append(builder, "customer_legacy_id", legacyOrder.CustomerLegacyId);
        Append(builder, "customer_name", legacyOrder.CustomerName);
        Append(builder, "customer_rfc", legacyOrder.CustomerRfc);
        Append(builder, "payment_condition", legacyOrder.PaymentCondition);
        Append(builder, "price_list_code", legacyOrder.PriceListCode);
        Append(builder, "delivery_type", legacyOrder.DeliveryType);
        Append(builder, "currency_code", legacyOrder.CurrencyCode);
        Append(builder, "subtotal", legacyOrder.Subtotal);
        Append(builder, "discount_total", legacyOrder.DiscountTotal);
        Append(builder, "tax_total", legacyOrder.TaxTotal);
        Append(builder, "total", legacyOrder.Total);

        foreach (var item in legacyOrder.Items
                     .OrderBy(x => x.LineNumber)
                     .ThenBy(x => x.LegacyArticleId, StringComparer.Ordinal)
                     .ThenBy(x => x.Description, StringComparer.Ordinal))
        {
            builder.Append("item_begin").Append('\n');
            Append(builder, "line_number", item.LineNumber);
            Append(builder, "legacy_article_id", item.LegacyArticleId);
            Append(builder, "sku", item.Sku);
            Append(builder, "description", item.Description);
            Append(builder, "unit_code", item.UnitCode);
            Append(builder, "unit_name", item.UnitName);
            Append(builder, "quantity", item.Quantity);
            Append(builder, "unit_price", item.UnitPrice);
            Append(builder, "discount_amount", item.DiscountAmount);
            Append(builder, "tax_rate", item.TaxRate);
            Append(builder, "tax_amount", item.TaxAmount);
            Append(builder, "line_total", item.LineTotal);
            Append(builder, "sat_product_service_code", item.SatProductServiceCode);
            Append(builder, "sat_unit_code", item.SatUnitCode);
            builder.Append("item_end").Append('\n');
        }

        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes);
    }

    private static void Append(StringBuilder builder, string key, string? value)
    {
        builder.Append(key)
            .Append('=')
            .Append(value is null ? "<null>" : Escape(value))
            .Append('\n');
    }

    private static void Append(StringBuilder builder, string key, decimal value)
    {
        builder.Append(key)
            .Append('=')
            .Append(value.ToString("G29", CultureInfo.InvariantCulture))
            .Append('\n');
    }

    private static void Append(StringBuilder builder, string key, int value)
    {
        builder.Append(key)
            .Append('=')
            .Append(value.ToString(CultureInfo.InvariantCulture))
            .Append('\n');
    }

    private static string Escape(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("=", "\\=", StringComparison.Ordinal);
    }
}
