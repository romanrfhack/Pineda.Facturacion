using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Text;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public static class ReceivablesSummaryComposer
{
    public const string IssuerLogoContentId = "issuer-logo";

    private const string ContentCellStyle = "padding:20px 24px 24px;";
    private const string SectionHeadingStyle = "margin:22px 0 10px;font-size:20px;color:#182533;font-family:Georgia,'Times New Roman',serif;";
    private const string DataTableStyle = "width:100%;border-collapse:collapse;margin-top:14px;font-family:Arial,sans-serif;font-size:13px;";
    private const string HeaderCellStyle = "background:#efe7d8;color:#3d3323;text-align:left;padding:10px;border-bottom:1px solid #eadfcb;";
    private const string DataCellStyle = "padding:10px;border-bottom:1px solid #eadfcb;vertical-align:top;";

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

    public static string BuildHtml(ReceivablesSummaryDocument document, bool renderIssuerLogoAsDataUri = false)
    {
        ArgumentNullException.ThrowIfNull(document);

        var builder = new StringBuilder();
        builder.AppendLine("<!doctype html>");
        builder.AppendLine("<html><head><meta charset=\"utf-8\"><style>");
        builder.AppendLine("body{margin:0;background:#f4f0e7;color:#182533;font-family:Georgia,'Times New Roman',serif;}");
        builder.AppendLine(".wrap{max-width:900px;margin:0 auto;padding:28px;}.panel{background:#fff;border:1px solid #d8d1c2;border-radius:18px;overflow:hidden;box-shadow:0 18px 40px rgba(24,37,51,.12);border-collapse:separate;}");
        builder.AppendLine(".hero{background:#182533;color:#fff;padding:28px 32px;}.hero small{color:#d8c7a0;letter-spacing:.12em;text-transform:uppercase}.hero h1{margin:8px 0 0;font-size:28px;}");
        builder.AppendLine(".content{padding:20px 24px 24px;}.metric{border:1px solid #e6dccb;border-radius:14px;padding:14px;background:#fffdf8;}.metric span{display:block;color:#6c5a38;font-size:12px;text-transform:uppercase;letter-spacing:.08em}.metric strong{display:block;margin-top:8px;font-size:20px;}");
        builder.AppendLine("table.data-table{width:100%;border-collapse:collapse;margin-top:14px;font-family:Arial,sans-serif;font-size:13px;}table.data-table th{background:#efe7d8;color:#3d3323;text-align:left;}table.data-table th,table.data-table td{padding:10px;border-bottom:1px solid #eadfcb;}tr.overdue{background:#fff4f1;}a{color:#174f78}.footer{padding:18px 24px;background:#faf7ef;color:#6b7280;font-size:12px;}");
        builder.AppendLine("</style></head><body><div class=\"wrap\" style=\"max-width:900px;margin:0 auto;padding:28px;\">");
        builder.AppendLine("<table role=\"presentation\" class=\"panel\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"background:#fff;border:1px solid #d8d1c2;border-radius:18px;overflow:hidden;box-shadow:0 18px 40px rgba(24,37,51,.12);border-collapse:separate;\">");
        builder.AppendLine("<tr><td class=\"hero\" style=\"background:#182533;color:#fff;padding:28px 32px;\">");
        AppendHeader(builder, document, renderIssuerLogoAsDataUri);
        builder.AppendLine("</td></tr>");
        builder.AppendLine("<tr><td class=\"content\" style=\"" + ContentCellStyle + "\">");
        builder.Append("<p style=\"margin:0 0 14px;line-height:1.55;\">").Append(Html(document.Message)).AppendLine("</p>");

        if (document.Format == ReceivablesSummaryFormat.Pdf)
        {
            builder.AppendLine("<p style=\"margin:0 0 14px;line-height:1.55;\">El estado de cuenta detallado se incluye como archivo PDF adjunto.</p>");
        }

        builder.AppendLine("<table role=\"presentation\" class=\"summary\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"width:100%;border-collapse:separate;border-spacing:0;margin:22px 0 10px;\"><tr>");
        AppendMetric(builder, "Facturas incluidas", document.Selection.InvoiceCount.ToString(CultureInfo.InvariantCulture));
        AppendMetric(builder, "Saldo total", FormatCurrencyTotals(document.Selection.TotalsByCurrency, x => x.OutstandingBalance));
        AppendMetric(builder, "Saldo vencido", FormatCurrencyTotals(document.Selection.TotalsByCurrency, x => x.OverdueBalance));
        AppendMetric(builder, "Saldo por vencer", FormatCurrencyTotals(document.Selection.TotalsByCurrency, x => x.CurrentBalance));
        builder.AppendLine("</tr></table>");

        if (document.IncludeOptions.TotalsByCurrency && document.Selection.TotalsByCurrency.Count > 1)
        {
            builder.Append("<h2 style=\"").Append(SectionHeadingStyle).AppendLine("\">Totales por moneda</h2>");
            builder.Append("<table class=\"data-table\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"").Append(DataTableStyle).AppendLine("\"><thead><tr>");
            AppendHeaderCell(builder, "Moneda");
            AppendHeaderCell(builder, "Facturas");
            AppendHeaderCell(builder, "Saldo");
            AppendHeaderCell(builder, "Vencido");
            AppendHeaderCell(builder, "Por vencer");
            builder.AppendLine("</tr></thead><tbody>");
            foreach (var total in document.Selection.TotalsByCurrency)
            {
                builder.Append("<tr>");
                AppendDataCell(builder, total.CurrencyCode);
                AppendDataCell(builder, total.InvoiceCount.ToString(CultureInfo.InvariantCulture));
                AppendDataCell(builder, FormatMoney(total.OutstandingBalance, total.CurrencyCode));
                AppendDataCell(builder, FormatMoney(total.OverdueBalance, total.CurrencyCode));
                AppendDataCell(builder, FormatMoney(total.CurrentBalance, total.CurrencyCode));
                builder.AppendLine("</tr>");
            }
            builder.AppendLine("</tbody></table>");
        }

        if (document.IncludeOptions.InvoiceTable && document.Format != ReceivablesSummaryFormat.Pdf)
        {
            AppendInvoiceTable(builder, document);
        }

        if (document.IncludeOptions.PaymentInstructions)
        {
            builder.Append("<h2 style=\"").Append(SectionHeadingStyle).AppendLine("\">Instrucciones de pago</h2>");
            builder.AppendLine("<p style=\"margin:0 0 14px;line-height:1.55;\">Favor de realizar el pago conforme a los acuerdos comerciales vigentes y compartir el comprobante para su conciliación. Si requiere una aclaración, responda a este correo indicando los folios involucrados.</p>");
        }

        if (document.IncludeOptions.ReceiverFiscalData || document.IncludeOptions.IssuerData)
        {
            builder.Append("<h2 style=\"").Append(SectionHeadingStyle).AppendLine("\">Datos fiscales</h2>");
            builder.Append("<table class=\"data-table\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"").Append(DataTableStyle).AppendLine("\"><tbody>");
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

        builder.AppendLine("</td></tr><tr><td class=\"footer\" style=\"padding:18px 24px;background:#faf7ef;color:#6b7280;font-size:12px;\">Resumen informativo generado con base en los registros actuales de cuentas por cobrar. Firma institucional.</td></tr>");
        builder.AppendLine("</table></div></body></html>");
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
        builder.Append("<td style=\"width:25%;padding:0 8px 12px 0;vertical-align:top;\">")
            .Append("<div class=\"metric\" style=\"border:1px solid #e6dccb;border-radius:14px;padding:14px;background:#fffdf8;\">")
            .Append("<span style=\"display:block;color:#6c5a38;font-size:12px;text-transform:uppercase;letter-spacing:.08em;\">")
            .Append(Html(label))
            .Append("</span><strong style=\"display:block;margin-top:8px;font-size:20px;color:#182533;\">")
            .Append(Html(value))
            .AppendLine("</strong></div></td>");
    }

    private static void AppendInvoiceTable(StringBuilder builder, ReceivablesSummaryDocument document)
    {
        builder.Append("<h2 style=\"").Append(SectionHeadingStyle).AppendLine("\">Facturas incluidas</h2>");
        builder.Append("<table class=\"data-table\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"").Append(DataTableStyle).AppendLine("\"><thead><tr>");
        AppendHeaderCell(builder, "Folio");
        AppendHeaderCell(builder, "Fecha factura");
        AppendHeaderCell(builder, "Vencimiento");
        AppendHeaderCell(builder, "Días vencida");
        AppendHeaderCell(builder, "Moneda");
        AppendHeaderCell(builder, "Total");
        AppendHeaderCell(builder, "Pagado");
        AppendHeaderCell(builder, "Saldo");
        AppendHeaderCell(builder, "Estado");
        if (document.IncludeOptions.InvoiceLinks)
        {
            AppendHeaderCell(builder, "Comprobante");
        }
        builder.AppendLine("</tr></thead><tbody>");

        foreach (var invoice in document.Invoices)
        {
            builder.Append("<tr");
            if (document.IncludeOptions.HighlightOverdue && invoice.IsOverdue)
            {
                builder.Append(" class=\"overdue\" style=\"background:#fff4f1;\"");
            }
            builder.Append(">");
            AppendDataCell(builder, FormatInvoiceLabel(invoice));
            AppendDataCell(builder, FormatDate(invoice.IssuedAtUtc));
            AppendDataCell(builder, FormatDate(invoice.DueAtUtc));
            AppendDataCell(builder, invoice.IsOverdue ? invoice.DaysPastDue.ToString(CultureInfo.InvariantCulture) : "-");
            AppendDataCell(builder, invoice.CurrencyCode);
            AppendDataCell(builder, FormatMoney(invoice.Total, invoice.CurrencyCode));
            AppendDataCell(builder, FormatMoney(invoice.PaidTotal, invoice.CurrencyCode));
            AppendDataCell(builder, FormatMoney(invoice.OutstandingBalance, invoice.CurrencyCode));
            AppendDataCell(builder, invoice.Status);

            if (document.IncludeOptions.InvoiceLinks)
            {
                builder.Append("<td style=\"").Append(DataCellStyle).Append("\">");
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
        builder.Append("<tr><th scope=\"row\" style=\"").Append(HeaderCellStyle).Append("\">").Append(Html(label)).Append("</th><td style=\"")
            .Append(DataCellStyle).Append("\">")
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

    private static void AppendHeader(StringBuilder builder, ReceivablesSummaryDocument document, bool renderIssuerLogoAsDataUri)
    {
        var logoSource = BuildIssuerLogoSource(document.IssuerLogo, renderIssuerLogoAsDataUri);
        if (logoSource is null)
        {
            AppendHeaderText(builder, document);
            return;
        }

        builder.AppendLine("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"width:100%;border-collapse:collapse;margin:0;font-family:Georgia,'Times New Roman',serif;\"><tr>");
        builder.AppendLine("<td style=\"vertical-align:top;padding:0;color:#fff;\">");
        AppendHeaderText(builder, document);
        builder.AppendLine("</td>");
        builder.Append("<td style=\"vertical-align:top;text-align:right;width:140px;padding:0 0 0 16px;\">")
            .Append("<img src=\"").Append(HtmlAttribute(logoSource)).Append("\" alt=\"Logo del emisor\" style=\"display:block;max-width:120px;max-height:48px;width:auto;height:auto;margin-left:auto;border:0;\" />")
            .AppendLine("</td>");
        builder.AppendLine("</tr></table>");
    }

    private static void AppendHeaderText(StringBuilder builder, ReceivablesSummaryDocument document)
    {
        builder.Append("<small style=\"color:#d8c7a0;letter-spacing:.12em;text-transform:uppercase;\">").Append(Html(document.Issuer.LegalName)).AppendLine("</small>");
        builder.AppendLine("<h1 style=\"margin:8px 0 0;font-size:28px;line-height:1.15;color:#fff;\">Resumen de adeudos pendientes</h1>");
        builder.Append("<p style=\"margin:10px 0 0;line-height:1.45;color:#fff;\">Emitido para ").Append(Html(document.Receiver.LegalName)).Append(" el ").Append(Html(FormatDateTime(document.GeneratedAtUtc))).AppendLine("</p>");
    }

    private static string? BuildIssuerLogoSource(ReceivablesSummaryLogo? logo, bool renderIssuerLogoAsDataUri)
    {
        if (logo?.Content is not { Length: > 0 })
        {
            return null;
        }

        if (!renderIssuerLogoAsDataUri)
        {
            return $"cid:{IssuerLogoContentId}";
        }

        var contentType = string.IsNullOrWhiteSpace(logo.ContentType) ? "application/octet-stream" : logo.ContentType;
        return $"data:{contentType};base64,{Convert.ToBase64String(logo.Content)}";
    }

    private static void AppendHeaderCell(StringBuilder builder, string label)
    {
        builder.Append("<th style=\"").Append(HeaderCellStyle).Append("\">").Append(Html(label)).Append("</th>");
    }

    private static void AppendDataCell(StringBuilder builder, string value)
    {
        builder.Append("<td style=\"").Append(DataCellStyle).Append("\">").Append(Html(value)).Append("</td>");
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
