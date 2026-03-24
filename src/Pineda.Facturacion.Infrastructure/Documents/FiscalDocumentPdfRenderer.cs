using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Pineda.Facturacion.Application.Abstractions.Documents;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.Documents;

public class FiscalDocumentPdfRenderer : IFiscalDocumentPdfRenderer
{
    public byte[] Render(FiscalDocument fiscalDocument, FiscalStamp fiscalStamp)
    {
        ArgumentNullException.ThrowIfNull(fiscalDocument);
        ArgumentNullException.ThrowIfNull(fiscalStamp);

        if (string.IsNullOrWhiteSpace(fiscalStamp.XmlContent))
        {
            throw new InvalidOperationException("Stamped XML is required to build the final CFDI PDF.");
        }

        var lines = BuildLines(fiscalDocument, fiscalStamp, XDocument.Parse(fiscalStamp.XmlContent, LoadOptions.PreserveWhitespace));
        return SimplePdfDocument.Create(lines);
    }

    private static IReadOnlyList<string> BuildLines(FiscalDocument fiscalDocument, FiscalStamp fiscalStamp, XDocument document)
    {
        var comprobante = document.Descendants().FirstOrDefault(static node => node.Name.LocalName == "Comprobante");
        var emisor = document.Descendants().FirstOrDefault(static node => node.Name.LocalName == "Emisor");
        var receptor = document.Descendants().FirstOrDefault(static node => node.Name.LocalName == "Receptor");
        var timbre = document.Descendants().FirstOrDefault(static node => node.Name.LocalName == "TimbreFiscalDigital");
        var conceptos = document.Descendants().Where(static node => node.Name.LocalName == "Concepto").ToList();

        var lines = new List<string>
        {
            "Representacion impresa del CFDI",
            string.Empty,
            $"UUID: {GetAttribute(timbre, "UUID") ?? fiscalStamp.Uuid ?? "N/D"}",
            $"Serie/Folio: {CombineDocumentNumber(GetAttribute(comprobante, "Serie"), GetAttribute(comprobante, "Folio"))}",
            $"Fecha de emision: {GetAttribute(comprobante, "Fecha") ?? FormatUtc(fiscalDocument.IssuedAtUtc)}",
            $"Fecha de timbrado: {GetAttribute(timbre, "FechaTimbrado") ?? FormatUtc(fiscalStamp.StampedAtUtc)}",
            $"Metodo de pago: {GetAttribute(comprobante, "MetodoPago") ?? fiscalDocument.PaymentMethodSat}",
            $"Forma de pago: {GetAttribute(comprobante, "FormaPago") ?? fiscalDocument.PaymentFormSat}",
            $"Moneda: {GetAttribute(comprobante, "Moneda") ?? fiscalDocument.CurrencyCode}",
            $"Lugar de expedicion: {GetAttribute(comprobante, "LugarExpedicion") ?? fiscalDocument.IssuerPostalCode}",
            string.Empty,
            "Emisor",
            $"RFC: {GetAttribute(emisor, "Rfc") ?? fiscalDocument.IssuerRfc}",
            $"Nombre: {GetAttribute(emisor, "Nombre") ?? fiscalDocument.IssuerLegalName}",
            $"Regimen fiscal: {GetAttribute(emisor, "RegimenFiscal") ?? fiscalDocument.IssuerFiscalRegimeCode}",
            string.Empty,
            "Receptor",
            $"RFC: {GetAttribute(receptor, "Rfc") ?? fiscalDocument.ReceiverRfc}",
            $"Nombre: {GetAttribute(receptor, "Nombre") ?? fiscalDocument.ReceiverLegalName}",
            $"Uso CFDI: {GetAttribute(receptor, "UsoCFDI") ?? fiscalDocument.ReceiverCfdiUseCode}",
            $"Regimen fiscal receptor: {GetAttribute(receptor, "RegimenFiscalReceptor") ?? fiscalDocument.ReceiverFiscalRegimeCode}",
            $"Codigo postal: {GetAttribute(receptor, "DomicilioFiscalReceptor") ?? fiscalDocument.ReceiverPostalCode}",
            string.Empty,
            "Conceptos"
        };

        if (conceptos.Count == 0)
        {
            lines.Add("No se encontraron conceptos en el XML timbrado.");
        }
        else
        {
            foreach (var concepto in conceptos)
            {
                lines.Add($"- {GetAttribute(concepto, "Descripcion") ?? "Concepto"}");
                lines.Add($"  Clave: {GetAttribute(concepto, "ClaveProdServ") ?? "N/D"} | Unidad: {GetAttribute(concepto, "ClaveUnidad") ?? "N/D"}");
                lines.Add($"  Cantidad: {GetAttribute(concepto, "Cantidad") ?? "0"} | Valor unitario: {GetAttribute(concepto, "ValorUnitario") ?? "0"}");
                lines.Add($"  Importe: {GetAttribute(concepto, "Importe") ?? "0"} | Descuento: {GetAttribute(concepto, "Descuento") ?? "0"}");
            }
        }

        lines.Add(string.Empty);
        lines.Add("Totales");
        lines.Add($"Subtotal: {GetAttribute(comprobante, "SubTotal") ?? fiscalDocument.Subtotal.ToString("0.00", CultureInfo.InvariantCulture)}");
        lines.Add($"Descuento: {GetAttribute(comprobante, "Descuento") ?? fiscalDocument.DiscountTotal.ToString("0.00", CultureInfo.InvariantCulture)}");
        lines.Add($"Impuestos: {ResolveTransferredTaxes(document, fiscalDocument).ToString("0.00", CultureInfo.InvariantCulture)}");
        lines.Add($"Total: {GetAttribute(comprobante, "Total") ?? fiscalDocument.Total.ToString("0.00", CultureInfo.InvariantCulture)}");

        if (!string.IsNullOrWhiteSpace(fiscalStamp.QrCodeTextOrUrl))
        {
            lines.Add(string.Empty);
            lines.Add($"QR / Representacion digital: {fiscalStamp.QrCodeTextOrUrl}");
        }

        if (!string.IsNullOrWhiteSpace(fiscalStamp.OriginalString))
        {
            lines.Add(string.Empty);
            lines.Add("Cadena original:");
            lines.Add(fiscalStamp.OriginalString);
        }

        if (!string.IsNullOrWhiteSpace(GetAttribute(timbre, "NoCertificadoSAT")))
        {
            lines.Add($"No. certificado SAT: {GetAttribute(timbre, "NoCertificadoSAT")}");
        }

        return lines;
    }

    private static decimal ResolveTransferredTaxes(XDocument document, FiscalDocument fiscalDocument)
    {
        var impuestos = document.Descendants().FirstOrDefault(static node => node.Name.LocalName == "Impuestos");
        var totalImpuestos = GetAttribute(impuestos, "TotalImpuestosTrasladados");
        return decimal.TryParse(totalImpuestos, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedTaxes)
            ? parsedTaxes
            : fiscalDocument.TaxTotal;
    }

    private static string CombineDocumentNumber(string? series, string? folio)
    {
        var value = $"{series}{folio}".Trim();
        return string.IsNullOrWhiteSpace(value) ? "N/D" : value;
    }

    private static string? GetAttribute(XElement? element, string localName)
    {
        return element?.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == localName)?.Value;
    }

    private static string FormatUtc(DateTime? value)
    {
        return value?.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture) ?? "N/D";
    }

    private static class SimplePdfDocument
    {
        private const int MaxLinesPerPage = 42;

        public static byte[] Create(IReadOnlyList<string> lines)
        {
            var normalizedLines = lines
                .SelectMany(WrapLine)
                .Select(NormalizePdfText)
                .ToList();

            var pages = normalizedLines
                .Chunk(MaxLinesPerPage)
                .Select(BuildContentStream)
                .ToList();

            var objects = new List<string>();
            var contentObjectIds = new List<int>();
            var pageObjectIds = new List<int>();

            objects.Add("<< /Type /Catalog /Pages 2 0 R >>");
            objects.Add(string.Empty);
            objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");

            foreach (var pageContent in pages)
            {
                contentObjectIds.Add(objects.Count + 1);
                objects.Add($"<< /Length {Encoding.ASCII.GetByteCount(pageContent)} >>\nstream\n{pageContent}\nendstream");
                pageObjectIds.Add(objects.Count + 1);
                objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 3 0 R >> >> /Contents {contentObjectIds[^1]} 0 R >>");
            }

            objects[1] = $"<< /Type /Pages /Count {pageObjectIds.Count} /Kids [{' '}{string.Join(' ', pageObjectIds.Select(id => $"{id} 0 R"))}] >>";

            var builder = new StringBuilder();
            builder.AppendLine("%PDF-1.4");
            var offsets = new List<int> { 0 };

            for (var index = 0; index < objects.Count; index++)
            {
                offsets.Add(Encoding.ASCII.GetByteCount(builder.ToString()));
                builder.Append(index + 1).AppendLine(" 0 obj");
                builder.AppendLine(objects[index]);
                builder.AppendLine("endobj");
            }

            var xrefPosition = Encoding.ASCII.GetByteCount(builder.ToString());
            builder.AppendLine("xref");
            builder.AppendLine($"0 {objects.Count + 1}");
            builder.AppendLine("0000000000 65535 f ");

            foreach (var offset in offsets.Skip(1))
            {
                builder.Append(offset.ToString("0000000000", CultureInfo.InvariantCulture)).AppendLine(" 00000 n ");
            }

            builder.AppendLine("trailer");
            builder.AppendLine($"<< /Size {objects.Count + 1} /Root 1 0 R >>");
            builder.AppendLine("startxref");
            builder.AppendLine(xrefPosition.ToString(CultureInfo.InvariantCulture));
            builder.Append("%%EOF");

            return Encoding.ASCII.GetBytes(builder.ToString());
        }

        private static string BuildContentStream(string[] lines)
        {
            var builder = new StringBuilder();
            builder.AppendLine("BT");
            builder.AppendLine("/F1 10 Tf");
            builder.AppendLine("40 760 Td");
            builder.AppendLine("14 TL");

            foreach (var line in lines)
            {
                builder.Append('(').Append(EscapePdfString(line)).AppendLine(") Tj");
                builder.AppendLine("T*");
            }

            builder.Append("ET");
            return builder.ToString();
        }

        private static IEnumerable<string> WrapLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                yield return " ";
                yield break;
            }

            const int maxLength = 95;
            var remaining = line.Trim();

            while (remaining.Length > maxLength)
            {
                var splitIndex = remaining.LastIndexOf(' ', maxLength);
                if (splitIndex <= 0)
                {
                    splitIndex = maxLength;
                }

                yield return remaining[..splitIndex].TrimEnd();
                remaining = remaining[splitIndex..].TrimStart();
            }

            yield return remaining;
        }

        private static string NormalizePdfText(string value)
        {
            var normalized = value.Normalize(NormalizationForm.FormKD);
            var builder = new StringBuilder(normalized.Length);

            foreach (var character in normalized)
            {
                if (character <= sbyte.MaxValue && !char.IsControl(character))
                {
                    builder.Append(character);
                }
            }

            return builder.ToString();
        }

        private static string EscapePdfString(string value)
        {
            return value
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("(", "\\(", StringComparison.Ordinal)
                .Replace(")", "\\)", StringComparison.Ordinal);
        }
    }
}
