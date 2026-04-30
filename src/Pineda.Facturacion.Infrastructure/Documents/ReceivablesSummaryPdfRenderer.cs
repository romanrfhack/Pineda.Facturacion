using System.Globalization;
using System.Text;
using Pineda.Facturacion.Application.Abstractions.Documents;
using Pineda.Facturacion.Application.UseCases.AccountsReceivable;

namespace Pineda.Facturacion.Infrastructure.Documents;

public sealed class ReceivablesSummaryPdfRenderer : IReceivablesSummaryPdfRenderer
{
    private const int MaxLineLength = 104;
    private const int MaxLinesPerPage = 48;

    public Task<byte[]> RenderAsync(ReceivablesSummaryDocument document, CancellationToken cancellationToken = default)
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

    private static IReadOnlyList<string> BuildLines(ReceivablesSummaryDocument document)
    {
        var lines = new List<string>
        {
            "RESUMEN DE ADEUDOS / ESTADO DE CUENTA",
            $"Fecha de emisión: {document.GeneratedAtUtc.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture)}",
            string.Empty,
            $"Emisor: {document.Issuer.LegalName} | RFC: {document.Issuer.Rfc} | Régimen: {document.Issuer.FiscalRegimeCode ?? "N/D"} | CP: {document.Issuer.PostalCode ?? "N/D"}",
            $"Receptor: {document.Receiver.LegalName} | RFC: {document.Receiver.Rfc} | Régimen: {document.Receiver.FiscalRegimeCode ?? "N/D"} | CP: {document.Receiver.PostalCode ?? "N/D"}",
            string.Empty,
            "Resumen ejecutivo de saldos:"
        };

        foreach (var total in document.Selection.TotalsByCurrency)
        {
            lines.Add($"  {total.CurrencyCode}: facturas {total.InvoiceCount} | saldo {ReceivablesSummaryComposer.FormatMoney(total.OutstandingBalance, total.CurrencyCode)} | vencido {ReceivablesSummaryComposer.FormatMoney(total.OverdueBalance, total.CurrencyCode)} | por vencer {ReceivablesSummaryComposer.FormatMoney(total.CurrentBalance, total.CurrencyCode)}");
        }

        lines.Add(string.Empty);
        lines.Add("Mensaje inicial:");
        lines.AddRange(Wrap(document.Message, MaxLineLength));

        if (document.IncludeOptions.PaymentInstructions)
        {
            lines.Add(string.Empty);
            lines.Add("Instrucciones de pago:");
            lines.AddRange(Wrap("Favor de realizar el pago conforme a los acuerdos comerciales vigentes y compartir el comprobante para su conciliación.", MaxLineLength));
        }

        if (document.IncludeOptions.InvoiceTable)
        {
            lines.Add(string.Empty);
            lines.Add("Detalle de facturas:");
            lines.Add("Folio | Fecha | Vencimiento | Días vencida | Moneda | Total | Pagado | Saldo | Estado");

            foreach (var invoice in document.Invoices)
            {
                var row = string.Join(" | ",
                    ReceivablesSummaryComposer.FormatInvoiceLabel(invoice),
                    ReceivablesSummaryComposer.FormatDate(invoice.IssuedAtUtc),
                    ReceivablesSummaryComposer.FormatDate(invoice.DueAtUtc),
                    invoice.IsOverdue ? invoice.DaysPastDue.ToString(CultureInfo.InvariantCulture) : "-",
                    invoice.CurrencyCode,
                    invoice.Total.ToString("N2", CultureInfo.InvariantCulture),
                    invoice.PaidTotal.ToString("N2", CultureInfo.InvariantCulture),
                    invoice.OutstandingBalance.ToString("N2", CultureInfo.InvariantCulture),
                    invoice.Status);
                lines.AddRange(Wrap(row, MaxLineLength));
            }
        }

        lines.Add(string.Empty);
        lines.Add("Pie de página: resumen informativo generado con base en los registros actuales de cuentas por cobrar.");

        return lines.SelectMany(line => Wrap(line, MaxLineLength)).ToArray();
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

            objects.Add((fontObjectId, Ascii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>")));
            objects = objects.OrderBy(x => x.Id).ToList();

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
            var maxObjectId = objects.Max(x => x.Id);
            WriteAscii(stream, $"xref\n0 {(maxObjectId + 1).ToString(CultureInfo.InvariantCulture)}\n");
            WriteAscii(stream, "0000000000 65535 f \n");
            for (var id = 1; id <= maxObjectId; id++)
            {
                var offset = offsets.TryGetValue(id, out var value) ? value : 0;
                WriteAscii(stream, $"{offset.ToString("0000000000", CultureInfo.InvariantCulture)} 00000 n \n");
            }

            WriteAscii(stream, $"trailer\n<< /Size {(maxObjectId + 1).ToString(CultureInfo.InvariantCulture)} /Root 1 0 R >>\nstartxref\n{xrefPosition.ToString(CultureInfo.InvariantCulture)}\n%%EOF");
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
            return Ascii(builder.ToString());
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
            return string.Concat(value.Select(character => character is >= ' ' and <= '~' ? character : RemoveAccent(character)));
        }

        private static char RemoveAccent(char character)
        {
            return character switch
            {
                'á' or 'Á' => 'a',
                'é' or 'É' => 'e',
                'í' or 'Í' => 'i',
                'ó' or 'Ó' => 'o',
                'ú' or 'Ú' => 'u',
                'ñ' or 'Ñ' => 'n',
                _ => '?'
            };
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
