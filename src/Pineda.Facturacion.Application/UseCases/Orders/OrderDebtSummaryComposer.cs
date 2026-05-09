using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Text;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Models.Legacy;

namespace Pineda.Facturacion.Application.UseCases.Orders;

public static class OrderDebtSummaryComposer
{
    private const string ContentCellStyle = "padding:20px 24px 24px;";
    private const string SectionHeadingStyle = "margin:22px 0 10px;font-size:20px;color:#182533;font-family:Georgia,'Times New Roman',serif;";
    private const string DataTableStyle = "width:100%;border-collapse:collapse;margin-top:14px;font-family:Arial,sans-serif;font-size:13px;";
    private const string HeaderCellStyle = "background:#efe7d8;color:#3d3323;text-align:left;padding:10px;border-bottom:1px solid #eadfcb;";
    private const string DataCellStyle = "padding:10px;border-bottom:1px solid #eadfcb;vertical-align:top;";

    public static bool TryParseFormat(string? value, out OrderDebtSummaryFormat format)
    {
        format = NormalizeKey(value) switch
        {
            "html" => OrderDebtSummaryFormat.Html,
            _ => OrderDebtSummaryFormat.Html
        };

        return NormalizeKey(value) is "html";
    }

    public static IReadOnlyList<string> NormalizeRecipients(IEnumerable<string>? recipients)
    {
        if (recipients is null)
        {
            return [];
        }

        var normalized = new List<string>();
        foreach (var recipient in recipients)
        {
            var candidate = recipient?.Trim();
            if (IsValidEmailAddress(candidate))
            {
                normalized.Add(new MailAddress(candidate!).Address);
            }
        }

        return normalized
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static bool IsValidEmailAddress(string? value)
    {
        var candidate = value?.Trim();
        if (string.IsNullOrWhiteSpace(candidate)
            || candidate.Contains('\r')
            || candidate.Contains('\n'))
        {
            return false;
        }

        try
        {
            var address = new MailAddress(candidate);
            return string.Equals(address.Address, candidate, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(address.User)
                && address.Host.Contains('.', StringComparison.Ordinal)
                && !address.Host.StartsWith(".", StringComparison.Ordinal)
                && !address.Host.EndsWith(".", StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static string BuildDefaultSubject(string receiverName)
    {
        var normalizedName = string.IsNullOrWhiteSpace(receiverName) ? "Receptor" : receiverName.Trim();
        return $"Resumen de notas pendientes - {normalizedName}";
    }

    public static string BuildDefaultMessage(string receiverName)
    {
        var normalizedName = string.IsNullOrWhiteSpace(receiverName) ? "cliente" : receiverName.Trim();
        return $"Estimado {normalizedName}, compartimos el resumen de notas/órdenes pendientes para su revisión. Favor de indicarnos cuáles desea que facturemos y confirmar cualquier aclaración sobre pago o datos fiscales.";
    }

    public static OrderDebtSummarySelection BuildSelectionSummary(IReadOnlyList<OrderDebtSummaryOrder> orders)
    {
        var totals = orders
            .GroupBy(order => NormalizeCurrency(order.CurrencyCode))
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new OrderDebtSummaryTotalByCurrency
            {
                CurrencyCode = group.Key,
                OrderCount = group.Count(),
                Total = group.Sum(order => order.Total)
            })
            .ToArray();

        return new OrderDebtSummarySelection
        {
            OrderCount = orders.Count,
            Total = totals.Length == 1 ? totals[0].Total : null,
            TotalsByCurrency = totals
        };
    }

    public static OrderDebtSummaryOrder MapOrder(
        LegacyOrderReadModel legacyOrder,
        ImportedLegacyOrderLookupModel? importedLookup)
    {
        ArgumentNullException.ThrowIfNull(legacyOrder);

        return new OrderDebtSummaryOrder
        {
            LegacyOrderId = legacyOrder.LegacyOrderId,
            OrderDateUtc = legacyOrder.OrderDateUtc,
            LegacyOrderNumber = string.IsNullOrWhiteSpace(legacyOrder.LegacyOrderNumber)
                ? legacyOrder.LegacyOrderId
                : legacyOrder.LegacyOrderNumber.Trim(),
            LegacyOrderType = legacyOrder.LegacyOrderType?.Trim(),
            CustomerName = legacyOrder.CustomerName.Trim(),
            CustomerRfc = legacyOrder.CustomerRfc?.Trim(),
            CurrencyCode = NormalizeCurrency(legacyOrder.CurrencyCode),
            Total = legacyOrder.Total,
            IsImported = importedLookup?.SalesOrderId.HasValue == true,
            SalesOrderId = importedLookup?.SalesOrderId,
            BillingDocumentId = importedLookup?.BillingDocumentId,
            BillingDocumentStatus = importedLookup?.BillingDocumentStatus,
            FiscalDocumentId = importedLookup?.FiscalDocumentId,
            FiscalDocumentStatus = importedLookup?.FiscalDocumentStatus,
            FiscalUuid = importedLookup?.FiscalUuid,
            ImportStatus = importedLookup?.ImportStatus,
            BillingStatusLabel = BuildBillingStatusLabel(importedLookup)
        };
    }

    public static string BuildHtml(OrderDebtSummaryDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var builder = new StringBuilder();
        builder.AppendLine("<!doctype html>");
        builder.AppendLine("<html><head><meta charset=\"utf-8\"><style>");
        builder.AppendLine("body{margin:0;background:#f4f0e7;color:#182533;font-family:Georgia,'Times New Roman',serif;}");
        builder.AppendLine(".wrap{max-width:900px;margin:0 auto;padding:28px;}.panel{background:#fff;border:1px solid #d8d1c2;border-radius:18px;overflow:hidden;box-shadow:0 18px 40px rgba(24,37,51,.12);border-collapse:separate;}");
        builder.AppendLine(".hero{background:#182533;color:#fff;padding:28px 32px;}.hero small{color:#d8c7a0;letter-spacing:.12em;text-transform:uppercase}.hero h1{margin:8px 0 0;font-size:28px;}");
        builder.AppendLine(".metric{border:1px solid #e6dccb;border-radius:14px;padding:14px;background:#fffdf8;}.metric span{display:block;color:#6c5a38;font-size:12px;text-transform:uppercase;letter-spacing:.08em}.metric strong{display:block;margin-top:8px;font-size:20px;}");
        builder.AppendLine("table.data-table{width:100%;border-collapse:collapse;margin-top:14px;font-family:Arial,sans-serif;font-size:13px;}table.data-table th{background:#efe7d8;color:#3d3323;text-align:left;}table.data-table th,table.data-table td{padding:10px;border-bottom:1px solid #eadfcb;}a{color:#174f78}.footer{padding:18px 24px;background:#faf7ef;color:#6b7280;font-size:12px;}");
        builder.AppendLine("</style></head><body><div class=\"wrap\" style=\"max-width:900px;margin:0 auto;padding:28px;\">");
        builder.AppendLine("<table role=\"presentation\" class=\"panel\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"background:#fff;border:1px solid #d8d1c2;border-radius:18px;overflow:hidden;box-shadow:0 18px 40px rgba(24,37,51,.12);border-collapse:separate;\">");
        builder.AppendLine("<tr><td class=\"hero\" style=\"background:#182533;color:#fff;padding:28px 32px;\">");
        AppendHeader(builder, document);
        builder.AppendLine("</td></tr>");
        builder.AppendLine("<tr><td class=\"content\" style=\"" + ContentCellStyle + "\">");
        builder.Append("<p style=\"margin:0 0 14px;line-height:1.6;\">").Append(HtmlWithLineBreaks(document.Message)).AppendLine("</p>");

        builder.AppendLine("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"width:100%;border-collapse:separate;border-spacing:0;margin:22px 0 10px;\"><tr>");
        AppendMetric(builder, "Órdenes incluidas", document.Selection.OrderCount.ToString(CultureInfo.InvariantCulture));
        AppendMetric(builder, "Total seleccionado", FormatCurrencyTotals(document.Selection.TotalsByCurrency));
        AppendMetric(builder, "Moneda", BuildCurrencyLabel(document.Selection.TotalsByCurrency));
        builder.AppendLine("</tr></table>");

        if (document.Options.IncludeTotals && document.Selection.TotalsByCurrency.Count > 1)
        {
            builder.Append("<h2 style=\"").Append(SectionHeadingStyle).AppendLine("\">Totales por moneda</h2>");
            builder.Append("<table class=\"data-table\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"").Append(DataTableStyle).AppendLine("\"><thead><tr>");
            AppendHeaderCell(builder, "Moneda");
            AppendHeaderCell(builder, "Órdenes");
            AppendHeaderCell(builder, "Total");
            builder.AppendLine("</tr></thead><tbody>");

            foreach (var total in document.Selection.TotalsByCurrency)
            {
                builder.Append("<tr>");
                AppendDataCell(builder, total.CurrencyCode);
                AppendDataCell(builder, total.OrderCount.ToString(CultureInfo.InvariantCulture));
                AppendDataCell(builder, FormatMoney(total.Total, total.CurrencyCode));
                builder.AppendLine("</tr>");
            }

            builder.AppendLine("</tbody></table>");
        }

        if (document.Options.IncludeOrderTable)
        {
            AppendOrdersTable(builder, document);
        }

        if (document.Options.IncludePaymentInstructions)
        {
            builder.Append("<h2 style=\"").Append(SectionHeadingStyle).AppendLine("\">Instrucciones y seguimiento</h2>");
            builder.AppendLine("<p style=\"margin:0 0 14px;line-height:1.55;\">Agradecemos responder este correo indicando cuáles órdenes o notas desean facturar y cualquier aclaración pendiente sobre pago o datos fiscales.</p>");
        }

        if (document.Options.IncludeReceiverFiscalData || document.Options.IncludeIssuerData)
        {
            builder.Append("<h2 style=\"").Append(SectionHeadingStyle).AppendLine("\">Datos fiscales</h2>");
            builder.Append("<table class=\"data-table\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"").Append(DataTableStyle).AppendLine("\"><tbody>");
            if (document.Options.IncludeIssuerData)
            {
                AppendPartyRow(builder, "Emisor", document.Issuer);
            }

            if (document.Options.IncludeReceiverFiscalData)
            {
                AppendPartyRow(builder, "Receptor", document.Receiver);
            }

            builder.AppendLine("</tbody></table>");
        }

        builder.AppendLine("</td></tr><tr><td class=\"footer\" style=\"padding:18px 24px;background:#faf7ef;color:#6b7280;font-size:12px;\">Resumen informativo generado con base en las órdenes/notas seleccionadas al momento de la emisión.</td></tr>");
        builder.AppendLine("</table></div></body></html>");
        return builder.ToString();
    }

    public static string FormatMoney(decimal amount, string currencyCode)
    {
        var currency = NormalizeCurrency(currencyCode);
        return $"{amount.ToString("N2", CultureInfo.InvariantCulture)} {currency}";
    }

    public static string FormatDate(DateTime value)
    {
        return value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static string BuildBillingStatusLabel(ImportedLegacyOrderLookupModel? importedLookup)
    {
        if (importedLookup?.FiscalDocumentId is not null)
        {
            return string.IsNullOrWhiteSpace(importedLookup.FiscalDocumentStatus)
                ? "Con comprobante fiscal"
                : $"Fiscal: {importedLookup.FiscalDocumentStatus.Trim()}";
        }

        if (importedLookup?.BillingDocumentId is not null)
        {
            return string.IsNullOrWhiteSpace(importedLookup.BillingDocumentStatus)
                ? "Con documento de facturación"
                : $"Facturación: {importedLookup.BillingDocumentStatus.Trim()}";
        }

        if (importedLookup?.SalesOrderId is not null || !string.IsNullOrWhiteSpace(importedLookup?.ImportStatus))
        {
            return "Importada";
        }

        return "Pendiente";
    }

    private static string NormalizeCurrency(string? currencyCode)
    {
        return string.IsNullOrWhiteSpace(currencyCode)
            ? "MXN"
            : currencyCode.Trim().ToUpperInvariant();
    }

    private static string NormalizeKey(string? value)
    {
        return (value ?? string.Empty)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Trim()
            .ToLowerInvariant();
    }

    private static void AppendHeader(StringBuilder builder, OrderDebtSummaryDocument document)
    {
        builder.Append("<small style=\"color:#d8c7a0;letter-spacing:.12em;text-transform:uppercase;\">")
            .Append(Html(document.Issuer.LegalName))
            .AppendLine("</small>");
        builder.AppendLine("<h1 style=\"margin:8px 0 0;font-size:28px;line-height:1.15;color:#fff;\">Resumen de notas pendientes</h1>");
        builder.Append("<p style=\"margin:10px 0 0;line-height:1.45;color:#fff;\">Emitido para ")
            .Append(Html(document.Receiver.LegalName))
            .Append(" el ")
            .Append(Html(FormatDateTime(document.GeneratedAtUtc)))
            .AppendLine("</p>");
    }

    private static void AppendMetric(StringBuilder builder, string label, string value)
    {
        builder.Append("<td style=\"width:33.33%;padding:0 8px 12px 0;vertical-align:top;\">")
            .Append("<div class=\"metric\" style=\"border:1px solid #e6dccb;border-radius:14px;padding:14px;background:#fffdf8;\">")
            .Append("<span style=\"display:block;color:#6c5a38;font-size:12px;text-transform:uppercase;letter-spacing:.08em;\">")
            .Append(Html(label))
            .Append("</span><strong style=\"display:block;margin-top:8px;font-size:20px;color:#182533;\">")
            .Append(Html(value))
            .AppendLine("</strong></div></td>");
    }

    private static void AppendOrdersTable(StringBuilder builder, OrderDebtSummaryDocument document)
    {
        builder.Append("<h2 style=\"").Append(SectionHeadingStyle).AppendLine("\">Órdenes / notas incluidas</h2>");
        builder.Append("<table class=\"data-table\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"").Append(DataTableStyle).AppendLine("\"><thead><tr>");
        AppendHeaderCell(builder, "Orden / Nota / Pedido");
        AppendHeaderCell(builder, "Fecha");
        AppendHeaderCell(builder, "Cliente / referencia");
        AppendHeaderCell(builder, "Moneda");
        AppendHeaderCell(builder, "Total");
        if (document.Options.IncludeBillingStatus)
        {
            AppendHeaderCell(builder, "Estado de facturación");
        }

        builder.AppendLine("</tr></thead><tbody>");

        foreach (var order in document.Orders)
        {
            builder.Append("<tr>");
            AppendDataCell(builder, BuildOrderLabel(order));
            AppendDataCell(builder, FormatDate(order.OrderDateUtc));
            AppendDataCell(builder, order.CustomerName);
            AppendDataCell(builder, order.CurrencyCode);
            AppendDataCell(builder, FormatMoney(order.Total, order.CurrencyCode));
            if (document.Options.IncludeBillingStatus)
            {
                AppendDataCell(builder, order.BillingStatusLabel);
            }

            builder.AppendLine("</tr>");
        }

        builder.AppendLine("</tbody></table>");
    }

    private static void AppendPartyRow(StringBuilder builder, string label, OrderDebtSummaryParty party)
    {
        builder.Append("<tr><th scope=\"row\" style=\"").Append(HeaderCellStyle).Append("\">").Append(Html(label)).Append("</th><td style=\"")
            .Append(DataCellStyle).Append("\">")
            .Append(Html(party.LegalName));

        if (!string.IsNullOrWhiteSpace(party.Rfc))
        {
            builder.Append(" · RFC ").Append(Html(party.Rfc));
        }

        if (!string.IsNullOrWhiteSpace(party.Email))
        {
            builder.Append(" · ").Append(Html(party.Email));
        }

        if (!string.IsNullOrWhiteSpace(party.FiscalRegimeCode))
        {
            builder.Append(" · Régimen ").Append(Html(party.FiscalRegimeCode));
        }

        if (!string.IsNullOrWhiteSpace(party.PostalCode))
        {
            builder.Append(" · CP ").Append(Html(party.PostalCode));
        }

        builder.AppendLine("</td></tr>");
    }

    private static void AppendHeaderCell(StringBuilder builder, string label)
    {
        builder.Append("<th style=\"").Append(HeaderCellStyle).Append("\">").Append(Html(label)).Append("</th>");
    }

    private static void AppendDataCell(StringBuilder builder, string value)
    {
        builder.Append("<td style=\"").Append(DataCellStyle).Append("\">").Append(Html(value)).Append("</td>");
    }

    private static string BuildOrderLabel(OrderDebtSummaryOrder order)
    {
        var type = string.IsNullOrWhiteSpace(order.LegacyOrderType) ? null : order.LegacyOrderType.Trim();
        return string.IsNullOrWhiteSpace(type)
            ? order.LegacyOrderNumber
            : $"{order.LegacyOrderNumber} ({type})";
    }

    private static string BuildCurrencyLabel(IReadOnlyList<OrderDebtSummaryTotalByCurrency> totals)
    {
        return totals.Count switch
        {
            0 => "MXN",
            1 => totals[0].CurrencyCode,
            _ => "Múltiples"
        };
    }

    private static string FormatCurrencyTotals(IReadOnlyList<OrderDebtSummaryTotalByCurrency> totals)
    {
        if (totals.Count == 0)
        {
            return FormatMoney(0m, "MXN");
        }

        return string.Join(" · ", totals.Select(total => FormatMoney(total.Total, total.CurrencyCode)));
    }

    private static string FormatDateTime(DateTime value)
    {
        return value.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
    }

    private static string Html(string? value)
    {
        return WebUtility.HtmlEncode(value ?? string.Empty);
    }

    private static string HtmlWithLineBreaks(string? value)
    {
        return Html(value)
            .Replace("\r\n", "<br />", StringComparison.Ordinal)
            .Replace("\n", "<br />", StringComparison.Ordinal)
            .Replace("\r", "<br />", StringComparison.Ordinal);
    }
}
