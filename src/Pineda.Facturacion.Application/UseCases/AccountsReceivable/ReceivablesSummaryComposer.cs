using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Text;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public static class ReceivablesSummaryComposer
{
    public static bool TryParseScope(string? value, out ReceivablesSummaryScope scope)
    {
        scope = NormalizeKey(value) switch
        {
            "allpending" => ReceivablesSummaryScope.AllPending,
            "overdue" => ReceivablesSummaryScope.Overdue,
            "manual" => ReceivablesSummaryScope.Manual,
            "currentselection" => ReceivablesSummaryScope.CurrentSelection,
            _ => ReceivablesSummaryScope.AllPending
        };

        return NormalizeKey(value) is "allpending" or "overdue" or "manual" or "currentselection";
    }

    public static bool TryParseFormat(string? value, out ReceivablesSummaryFormat format)
    {
        format = NormalizeKey(value) switch
        {
            "html" => ReceivablesSummaryFormat.Html,
            "htmlwithpdf" => ReceivablesSummaryFormat.HtmlWithPdf,
            "pdf" => ReceivablesSummaryFormat.Pdf,
            _ => ReceivablesSummaryFormat.Html
        };

        return NormalizeKey(value) is "html" or "htmlwithpdf" or "pdf";
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
        return $"Resumen de adeudos pendientes - {normalizedName}";
    }

    public static string BuildDefaultMessage(string receiverName)
    {
        var normalizedName = string.IsNullOrWhiteSpace(receiverName) ? "cliente" : receiverName.Trim();
        return $"Estimado {normalizedName}, compartimos el resumen actualizado de adeudos pendientes para su revisión. Agradecemos validar la información y considerar las facturas vencidas con prioridad.";
    }

    public static ReceivablesSummarySelection BuildSelectionSummary(IReadOnlyList<ReceivablesSummaryCandidate> invoices)
    {
        var totals = invoices
            .GroupBy(x => string.IsNullOrWhiteSpace(x.CurrencyCode) ? "MXN" : x.CurrencyCode.Trim().ToUpperInvariant())
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ReceivablesSummaryTotalByCurrency
            {
                CurrencyCode = group.Key,
                InvoiceCount = group.Count(),
                Total = group.Sum(x => x.Total),
                PaidTotal = group.Sum(x => x.PaidTotal),
                OutstandingBalance = group.Sum(x => x.OutstandingBalance),
                OverdueBalance = group.Where(x => x.IsOverdue).Sum(x => x.OutstandingBalance),
                CurrentBalance = group.Where(x => !x.IsOverdue).Sum(x => x.OutstandingBalance)
            })
            .ToArray();

        return new ReceivablesSummarySelection
        {
            InvoiceCount = invoices.Count,
            OutstandingBalance = invoices.Sum(x => x.OutstandingBalance),
            OverdueBalance = invoices.Where(x => x.IsOverdue).Sum(x => x.OutstandingBalance),
            CurrentBalance = invoices.Where(x => !x.IsOverdue).Sum(x => x.OutstandingBalance),
            TotalsByCurrency = totals
        };
    }

    public static IReadOnlyList<ReceivablesSummaryCandidate> SelectInvoices(
        IReadOnlyList<ReceivablesSummaryCandidate> candidates,
        ReceivablesSummaryScope scope,
        IReadOnlyCollection<long> requestedInvoiceIds)
    {
        return scope switch
        {
            ReceivablesSummaryScope.AllPending => candidates,
            ReceivablesSummaryScope.Overdue => candidates.Where(x => x.IsOverdue).ToArray(),
            ReceivablesSummaryScope.Manual or ReceivablesSummaryScope.CurrentSelection => candidates
                .Where(x => requestedInvoiceIds.Contains(x.AccountsReceivableInvoiceId))
                .ToArray(),
            _ => []
        };
    }

    public static string BuildHtml(ReceivablesSummaryDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var builder = new StringBuilder();
        builder.AppendLine("<!doctype html>");
        builder.AppendLine("<html><head><meta charset=\"utf-8\"><style>");
        builder.AppendLine("body{margin:0;background:#f4f0e7;color:#182533;font-family:Georgia,'Times New Roman',serif;}");
        builder.AppendLine(".wrap{max-width:900px;margin:0 auto;padding:28px;}.panel{background:#fff;border:1px solid #d8d1c2;border-radius:18px;overflow:hidden;box-shadow:0 18px 40px rgba(24,37,51,.12);}");
        builder.AppendLine(".hero{background:#182533;color:#fff;padding:28px 32px;}.hero small{color:#d8c7a0;letter-spacing:.12em;text-transform:uppercase}.hero h1{margin:8px 0 0;font-size:28px;}");
        builder.AppendLine(".content{padding:20px 24px 24px;}.summary{display:grid;grid-template-columns:repeat(auto-fit,minmax(180px,1fr));gap:12px;margin:22px 0;}.metric{border:1px solid #e6dccb;border-radius:14px;padding:14px;background:#fffdf8;}.metric span{display:block;color:#6c5a38;font-size:12px;text-transform:uppercase;letter-spacing:.08em}.metric strong{display:block;margin-top:8px;font-size:20px;}");
        builder.AppendLine("table{width:100%;border-collapse:collapse;margin-top:14px;font-family:Arial,sans-serif;font-size:13px;}th{background:#efe7d8;color:#3d3323;text-align:left;}th,td{padding:10px;border-bottom:1px solid #eadfcb;}tr.overdue{background:#fff4f1;}a{color:#174f78}.footer{padding:18px 32px;background:#faf7ef;color:#6b7280;font-size:12px;}");
        builder.AppendLine("</style></head><body><div class=\"wrap\"><section class=\"panel\">");
        builder.AppendLine("<header class=\"hero\">");
        builder.Append("<small>").Append(Html(document.Issuer.LegalName)).AppendLine("</small>");
        builder.AppendLine("<h1>Resumen de adeudos pendientes</h1>");
        builder.Append("<p>Emitido para ").Append(Html(document.Receiver.LegalName)).Append(" el ").Append(Html(FormatDateTime(document.GeneratedAtUtc))).AppendLine("</p>");
        builder.AppendLine("</header><main class=\"content\" style=\"padding:20px 24px 24px;\">");
        builder.Append("<p>").Append(Html(document.Message)).AppendLine("</p>");

        if (document.Format == ReceivablesSummaryFormat.Pdf)
        {
            builder.AppendLine("<p>El estado de cuenta detallado se incluye como archivo PDF adjunto.</p>");
        }

        builder.AppendLine("<section class=\"summary\">");
        AppendMetric(builder, "Facturas incluidas", document.Selection.InvoiceCount.ToString(CultureInfo.InvariantCulture));
        AppendMetric(builder, "Saldo total", FormatCurrencyTotals(document.Selection.TotalsByCurrency, x => x.OutstandingBalance));
        AppendMetric(builder, "Saldo vencido", FormatCurrencyTotals(document.Selection.TotalsByCurrency, x => x.OverdueBalance));
        AppendMetric(builder, "Saldo por vencer", FormatCurrencyTotals(document.Selection.TotalsByCurrency, x => x.CurrentBalance));
        builder.AppendLine("</section>");

        if (document.IncludeOptions.TotalsByCurrency && document.Selection.TotalsByCurrency.Count > 1)
        {
            builder.AppendLine("<h2>Totales por moneda</h2><table><thead><tr><th>Moneda</th><th>Facturas</th><th>Saldo</th><th>Vencido</th><th>Por vencer</th></tr></thead><tbody>");
            foreach (var total in document.Selection.TotalsByCurrency)
            {
                builder.Append("<tr><td>").Append(Html(total.CurrencyCode)).Append("</td><td>").Append(total.InvoiceCount.ToString(CultureInfo.InvariantCulture)).Append("</td><td>")
                    .Append(Html(FormatMoney(total.OutstandingBalance, total.CurrencyCode))).Append("</td><td>")
                    .Append(Html(FormatMoney(total.OverdueBalance, total.CurrencyCode))).Append("</td><td>")
                    .Append(Html(FormatMoney(total.CurrentBalance, total.CurrencyCode))).AppendLine("</td></tr>");
            }
            builder.AppendLine("</tbody></table>");
        }

        if (document.IncludeOptions.InvoiceTable && document.Format != ReceivablesSummaryFormat.Pdf)
        {
            AppendInvoiceTable(builder, document);
        }

        if (document.IncludeOptions.PaymentInstructions)
        {
            builder.AppendLine("<h2>Instrucciones de pago</h2>");
            builder.AppendLine("<p>Favor de realizar el pago conforme a los acuerdos comerciales vigentes y compartir el comprobante para su conciliación. Si requiere una aclaración, responda a este correo indicando los folios involucrados.</p>");
        }

        if (document.IncludeOptions.ReceiverFiscalData || document.IncludeOptions.IssuerData)
        {
            builder.AppendLine("<h2>Datos fiscales</h2><table><tbody>");
            if (document.IncludeOptions.IssuerData)
            {
                AppendPartyRow(builder, "Emisor", document.Issuer);
            }
            if (document.IncludeOptions.ReceiverFiscalData)
            {
                AppendPartyRow(builder, "Receptor", document.Receiver);
            }
            builder.AppendLine("</tbody></table>");
        }

        builder.AppendLine("</main><footer class=\"footer\" style=\"padding:18px 24px;background:#faf7ef;color:#6b7280;font-size:12px;\">Resumen informativo generado con base en los registros actuales de cuentas por cobrar. Firma institucional.</footer>");
        builder.AppendLine("</section></div></body></html>");
        return builder.ToString();
    }

    public static string BuildPdfFileName(ReceivablesSummaryDocument document)
    {
        var receiverToken = SanitizeFileToken(document.Receiver.Rfc);
        var dateToken = document.GeneratedAtUtc.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        return $"resumen_adeudos_{receiverToken}_{dateToken}.pdf";
    }

    public static ReceivablesSummaryCandidate MapCandidate(
        AccountsReceivablePortfolioItem item,
        DateTime nowUtc)
    {
        var dueDate = item.DueAtUtc?.Date;
        var isOverdue = item.OutstandingBalance > 0m
            && dueDate.HasValue
            && dueDate.Value < nowUtc.Date;
        var daysPastDue = isOverdue ? Math.Max(0, (nowUtc.Date - dueDate!.Value).Days) : 0;
        var fiscalDocumentId = item.FiscalDocumentId;

        return new ReceivablesSummaryCandidate
        {
            AccountsReceivableInvoiceId = item.AccountsReceivableInvoiceId,
            FiscalDocumentId = fiscalDocumentId,
            FiscalSeries = item.FiscalSeries,
            FiscalFolio = item.FiscalFolio,
            FiscalUuid = item.FiscalUuid,
            IssuedAtUtc = item.IssuedAtUtc,
            DueAtUtc = item.DueAtUtc,
            DaysPastDue = daysPastDue,
            CurrencyCode = string.IsNullOrWhiteSpace(item.CurrencyCode) ? "MXN" : item.CurrencyCode,
            Total = item.Total,
            PaidTotal = item.PaidTotal,
            OutstandingBalance = item.OutstandingBalance,
            Status = item.Status,
            IsOverdue = isOverdue,
            DocumentLink = fiscalDocumentId.HasValue ? $"/api/fiscal-documents/{fiscalDocumentId.Value}/stamp/pdf" : null
        };
    }

    public static bool IsEligible(AccountsReceivablePortfolioItem item)
    {
        return item.OutstandingBalance > 0m
            && !string.Equals(item.Status, nameof(AccountsReceivableInvoiceStatus.Cancelled), StringComparison.OrdinalIgnoreCase)
            && !string.Equals(item.Status, nameof(AccountsReceivableInvoiceStatus.Paid), StringComparison.OrdinalIgnoreCase);
    }

    public static string FormatMoney(decimal amount, string currencyCode)
    {
        var currency = string.IsNullOrWhiteSpace(currencyCode) ? "MXN" : currencyCode.Trim().ToUpperInvariant();
        return $"{amount.ToString("N2", CultureInfo.InvariantCulture)} {currency}";
    }

    public static string FormatInvoiceLabel(ReceivablesSummaryCandidate invoice)
    {
        var series = invoice.FiscalSeries?.Trim();
        var folio = invoice.FiscalFolio?.Trim();
        if (!string.IsNullOrWhiteSpace(series) || !string.IsNullOrWhiteSpace(folio))
        {
            return string.Join("-", new[] { series, folio }.Where(x => !string.IsNullOrWhiteSpace(x)));
        }

        return invoice.FiscalDocumentId.HasValue
            ? $"CFDI #{invoice.FiscalDocumentId.Value}"
            : $"CxC #{invoice.AccountsReceivableInvoiceId}";
    }

    public static string FormatDate(DateTime? value, string fallback = "Sin fecha de vencimiento")
    {
        return value.HasValue ? value.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : fallback;
    }

    private static string NormalizeKey(string? value)
    {
        return (value ?? string.Empty)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Trim()
            .ToLowerInvariant();
    }

    private static void AppendMetric(StringBuilder builder, string label, string value)
    {
        builder.Append("<article class=\"metric\"><span>").Append(Html(label)).Append("</span><strong>")
            .Append(Html(value)).AppendLine("</strong></article>");
    }

    private static void AppendInvoiceTable(StringBuilder builder, ReceivablesSummaryDocument document)
    {
        builder.AppendLine("<h2>Facturas incluidas</h2>");
        builder.AppendLine("<table><thead><tr><th>Folio</th><th>Fecha factura</th><th>Vencimiento</th><th>Días vencida</th><th>Moneda</th><th>Total</th><th>Pagado</th><th>Saldo</th><th>Estado</th>");
        if (document.IncludeOptions.InvoiceLinks)
        {
            builder.Append("<th>Comprobante</th>");
        }
        builder.AppendLine("</tr></thead><tbody>");

        foreach (var invoice in document.Invoices)
        {
            builder.Append("<tr");
            if (document.IncludeOptions.HighlightOverdue && invoice.IsOverdue)
            {
                builder.Append(" class=\"overdue\"");
            }
            builder.Append("><td>").Append(Html(FormatInvoiceLabel(invoice))).Append("</td><td>")
                .Append(Html(FormatDate(invoice.IssuedAtUtc))).Append("</td><td>")
                .Append(Html(FormatDate(invoice.DueAtUtc))).Append("</td><td>")
                .Append(invoice.IsOverdue ? invoice.DaysPastDue.ToString(CultureInfo.InvariantCulture) : "-").Append("</td><td>")
                .Append(Html(invoice.CurrencyCode)).Append("</td><td>")
                .Append(Html(FormatMoney(invoice.Total, invoice.CurrencyCode))).Append("</td><td>")
                .Append(Html(FormatMoney(invoice.PaidTotal, invoice.CurrencyCode))).Append("</td><td>")
                .Append(Html(FormatMoney(invoice.OutstandingBalance, invoice.CurrencyCode))).Append("</td><td>")
                .Append(Html(invoice.Status)).Append("</td>");

            if (document.IncludeOptions.InvoiceLinks)
            {
                builder.Append("<td>");
                if (!string.IsNullOrWhiteSpace(invoice.DocumentLink))
                {
                    builder.Append("<a href=\"").Append(HtmlAttribute(invoice.DocumentLink)).Append("\">Ver CFDI</a>");
                }
                else
                {
                    builder.Append("No disponible");
                }
                builder.Append("</td>");
            }

            builder.AppendLine("</tr>");
        }

        builder.AppendLine("</tbody></table>");
    }

    private static void AppendPartyRow(StringBuilder builder, string label, ReceivablesSummaryParty party)
    {
        builder.Append("<tr><th>").Append(Html(label)).Append("</th><td>")
            .Append(Html(party.LegalName)).Append(" · RFC ").Append(Html(party.Rfc));
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

    private static string FormatCurrencyTotals(
        IReadOnlyList<ReceivablesSummaryTotalByCurrency> totals,
        Func<ReceivablesSummaryTotalByCurrency, decimal> selector)
    {
        return string.Join(" · ", totals.Select(total => FormatMoney(selector(total), total.CurrencyCode)));
    }

    private static string FormatDateTime(DateTime value)
    {
        return value.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
    }

    private static string Html(string? value)
    {
        return WebUtility.HtmlEncode(value ?? string.Empty);
    }

    private static string HtmlAttribute(string? value)
    {
        return Html(value).Replace("\"", "&quot;", StringComparison.Ordinal);
    }

    private static string SanitizeFileToken(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "receptor" : value.Trim();
        var chars = normalized
            .Select(character => char.IsLetterOrDigit(character) ? character : '_')
            .ToArray();
        return new string(chars).Trim('_').ToLowerInvariant();
    }
}
