using System.Globalization;
using System.Text;
using Pineda.Facturacion.Application.Abstractions.Documents;
using Pineda.Facturacion.Application.UseCases.Orders;

namespace Pineda.Facturacion.Infrastructure.Documents;

public sealed class OrderDebtSummaryPdfRenderer : IOrderDebtSummaryPdfRenderer
{
    private const int MaxLineLength = 104;
    private const int MaxLinesPerPage = 48;

    public Task<byte[]> RenderAsync(
        OrderDebtSummaryDocument document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();

        var lines = BuildLines(document);
        var pages = lines
            .Chunk(MaxLinesPerPage)
            .Select(pageLines => pageLines.ToArray())
            .ToArray();

        return Task.FromResult(SimplePdfDocument.Create(pages.Length == 0 ? [[]] : pages));
    }

    private static IReadOnlyList<string> BuildLines(OrderDebtSummaryDocument document)
    {
        var lines = new List<string>();

        AddLine(lines, document.Issuer.LegalName);
        AddLine(lines, "RESUMEN DE NOTAS PENDIENTES");
        AddLine(
            lines,
            $"Emitido para {document.Receiver.LegalName} | {OrderDebtSummaryComposer.FormatGeneratedAt(document.GeneratedAtUtc)}");
        AddLine(
            lines,
            $"Órdenes incluidas: {document.Selection.OrderCount.ToString(CultureInfo.InvariantCulture)} | " +
            $"Total seleccionado: {FormatCurrencyTotals(document.Selection.TotalsByCurrency)} | " +
            $"Moneda: {BuildCurrencyLabel(document.Selection.TotalsByCurrency)}");
        AddLine(lines, string.Empty);
        AddLine(lines, document.Message);

        if (document.Options.IncludeTotals && document.Selection.TotalsByCurrency.Count > 1)
        {
            AddLine(lines, string.Empty);
            AddLine(lines, "TOTALES POR MONEDA");
            foreach (var total in document.Selection.TotalsByCurrency)
            {
                AddLine(
                    lines,
                    $"{total.CurrencyCode} | Órdenes: {total.OrderCount.ToString(CultureInfo.InvariantCulture)} | " +
                    $"Total: {OrderDebtSummaryComposer.FormatMoney(total.Total, total.CurrencyCode)}");
            }
        }

        if (document.Options.IncludeOrderTable)
        {
            AddLine(lines, string.Empty);
            AddLine(lines, "ÓRDENES / NOTAS INCLUIDAS");
            AddLine(
                lines,
                document.Options.IncludeBillingStatus
                    ? "Orden / Nota / Pedido | Fecha | Cliente / referencia | Moneda | Total | Estado"
                    : "Orden / Nota / Pedido | Fecha | Cliente / referencia | Moneda | Total");

            foreach (var order in document.Orders)
            {
                var values = new List<string>
                {
                    BuildOrderLabel(order),
                    OrderDebtSummaryComposer.FormatDate(order.OrderDateUtc),
                    order.CustomerName,
                    order.CurrencyCode,
                    OrderDebtSummaryComposer.FormatMoney(order.Total, order.CurrencyCode)
                };

                if (document.Options.IncludeBillingStatus)
                {
                    values.Add(order.BillingStatusLabel);
                }

                AddLine(lines, string.Join(" | ", values));
            }
        }

        if (document.Options.IncludePaymentInstructions)
        {
            AddLine(lines, string.Empty);
            AddLine(lines, "INSTRUCCIONES Y SEGUIMIENTO");
            AddLine(
                lines,
                "Agradecemos responder indicando cuáles órdenes o notas desean facturar y cualquier aclaración " +
                "pendiente sobre pago o datos fiscales.");
        }

        if (document.Options.IncludeIssuerData || document.Options.IncludeReceiverFiscalData)
        {
            AddLine(lines, string.Empty);
            AddLine(lines, "DATOS FISCALES");

            if (document.Options.IncludeIssuerData)
            {
                AddLine(lines, FormatParty("Emisor", document.Issuer));
            }

            if (document.Options.IncludeReceiverFiscalData)
            {
                AddLine(lines, FormatParty("Receptor", document.Receiver));
            }
        }

        AddLine(lines, string.Empty);
        AddLine(
            lines,
            "Resumen informativo generado con base en las órdenes/notas seleccionadas al momento de la emisión.");
        return lines;
    }

    private static void AddLine(ICollection<string> lines, string? value)
    {
        foreach (var line in Wrap(value, MaxLineLength))
        {
            lines.Add(line);
        }
    }

    private static string BuildOrderLabel(OrderDebtSummaryOrder order)
    {
        return string.IsNullOrWhiteSpace(order.LegacyOrderType)
            ? order.LegacyOrderNumber
            : $"{order.LegacyOrderNumber} ({order.LegacyOrderType.Trim()})";
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
        return totals.Count == 0
            ? OrderDebtSummaryComposer.FormatMoney(0m, "MXN")
            : string.Join(
                " | ",
                totals.Select(total => OrderDebtSummaryComposer.FormatMoney(total.Total, total.CurrencyCode)));
    }

    private static string FormatParty(string label, OrderDebtSummaryParty party)
    {
        var values = new List<string> { $"{label}: {party.LegalName}", $"RFC: {party.Rfc}" };
        if (!string.IsNullOrWhiteSpace(party.FiscalRegimeCode))
        {
            values.Add($"Régimen: {party.FiscalRegimeCode}");
        }

        if (!string.IsNullOrWhiteSpace(party.PostalCode))
        {
            values.Add($"CP: {party.PostalCode}");
        }

        if (!string.IsNullOrWhiteSpace(party.Email))
        {
            values.Add($"Correo: {party.Email}");
        }

        return string.Join(" | ", values);
    }

    private static IEnumerable<string> Wrap(string? value, int maxLength)
    {
        var text = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        if (text.Length <= maxLength)
        {
            yield return text;
            yield break;
        }

        while (text.Length > maxLength)
        {
            var splitAt = text.LastIndexOf(' ', maxLength);
            if (splitAt <= 0)
            {
                splitAt = maxLength;
            }

            yield return text[..splitAt].Trim();
            text = text[splitAt..].Trim();
        }

        if (text.Length > 0)
        {
            yield return text;
        }
    }

    private static class SimplePdfDocument
    {
        public static byte[] Create(IReadOnlyList<IReadOnlyList<string>> pages)
        {
            var pageCount = pages.Count;
            var fontObjectId = 3 + (pageCount * 2);
            var objects = new List<(int Id, byte[] Content)>
            {
                (1, Ascii("<< /Type /Catalog /Pages 2 0 R >>"))
            };

            var kids = string.Join(" ", Enumerable.Range(0, pageCount).Select(index => $"{3 + (index * 2)} 0 R"));
            objects.Add((2, Ascii($"<< /Type /Pages /Kids [{kids}] /Count {pageCount.ToString(CultureInfo.InvariantCulture)} >>")));

            for (var index = 0; index < pageCount; index++)
            {
                var pageObjectId = 3 + (index * 2);
                var contentObjectId = pageObjectId + 1;
                var content = BuildContentStream(pages[index]);
                objects.Add((pageObjectId, Ascii($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 {fontObjectId} 0 R >> >> /Contents {contentObjectId} 0 R >>")));
                objects.Add((contentObjectId, BuildStreamObject(content)));
            }

            objects.Add((fontObjectId, Ascii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>")));
            objects = objects.OrderBy(item => item.Id).ToList();

            using var stream = new MemoryStream();
            WriteAscii(stream, "%PDF-1.4\n");
            var offsets = new Dictionary<int, long>();
            foreach (var item in objects)
            {
                offsets[item.Id] = stream.Position;
                WriteAscii(stream, $"{item.Id} 0 obj\n");
                stream.Write(item.Content);
                WriteAscii(stream, "\nendobj\n");
            }

            var xrefPosition = stream.Position;
            var maxObjectId = objects.Max(item => item.Id);
            WriteAscii(stream, $"xref\n0 {(maxObjectId + 1).ToString(CultureInfo.InvariantCulture)}\n");
            WriteAscii(stream, "0000000000 65535 f \n");
            for (var id = 1; id <= maxObjectId; id++)
            {
                var offset = offsets.TryGetValue(id, out var value) ? value : 0;
                WriteAscii(stream, $"{offset.ToString("0000000000", CultureInfo.InvariantCulture)} 00000 n \n");
            }

            WriteAscii(
                stream,
                $"trailer\n<< /Size {(maxObjectId + 1).ToString(CultureInfo.InvariantCulture)} /Root 1 0 R >>\n" +
                $"startxref\n{xrefPosition.ToString(CultureInfo.InvariantCulture)}\n%%EOF");
            return stream.ToArray();
        }

        private static byte[] BuildContentStream(IReadOnlyList<string> lines)
        {
            var builder = new StringBuilder();
            builder.AppendLine("BT");
            builder.AppendLine("/F1 10 Tf");
            builder.AppendLine("50 760 Td");
            builder.AppendLine("13 TL");

            foreach (var line in lines)
            {
                builder.Append('(').Append(EscapePdfLiteral(ToPdfSafeText(line))).AppendLine(") Tj");
                builder.AppendLine("T*");
            }

            builder.AppendLine("ET");
            return Encoding.Latin1.GetBytes(builder.ToString());
        }

        private static byte[] BuildStreamObject(byte[] content)
        {
            using var stream = new MemoryStream();
            WriteAscii(stream, $"<< /Length {content.Length.ToString(CultureInfo.InvariantCulture)} >>\nstream\n");
            stream.Write(content);
            WriteAscii(stream, "endstream");
            return stream.ToArray();
        }

        private static string EscapePdfLiteral(string value)
        {
            return value
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("(", "\\(", StringComparison.Ordinal)
                .Replace(")", "\\)", StringComparison.Ordinal);
        }

        private static string ToPdfSafeText(string value)
        {
            return string.Concat(value.Select(character => character is >= ' ' and <= '\u00ff' ? character : '?'));
        }

        private static byte[] Ascii(string value)
        {
            return Encoding.ASCII.GetBytes(value);
        }

        private static void WriteAscii(Stream stream, string value)
        {
            stream.Write(Ascii(value));
        }
    }
}
