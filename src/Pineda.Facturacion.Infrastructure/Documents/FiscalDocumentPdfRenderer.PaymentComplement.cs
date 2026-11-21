using System.Globalization;

namespace Pineda.Facturacion.Infrastructure.Documents;

public sealed partial class FiscalDocumentPdfRenderer
{
    // REP-only layout. Invoice rendering and the public PDF endpoint remain unchanged.
    private sealed partial class FiscalPdfDocument
    {
        private static readonly PdfColor RepBlue = new(24, 58, 86);
        private static readonly PdfColor RepGold = new(138, 106, 50);
        private static readonly PdfColor RepInk = new(50, 58, 70);
        private static readonly PdfColor RepRule = new(204, 213, 219);
        private float RepWidth => PageWidth - Margin * 2;

        private byte[] BuildPaymentComplement(PdfViewModel model, RepAdditionalLayout rep, PdfImageAsset? logo, PdfImageAsset? qr)
        {
            DrawRepHeading(model, logo);
            DrawRepParties(model);

            var amount = rep.ComplementFields.FirstOrDefault(field => field.Label == "Monto total de pagos")?.Value ?? "N/D";
            EnsureSpace(65f);
            _page.DrawLine(Margin, _cursorY, PageWidth - Margin, _cursorY, RepGold, 1f);
            _page.DrawText("Monto total de pagos / MXN", Margin, _cursorY - 17f, 9f, PdfFont.Bold, RepBlue);
            var amountLabel = decimal.TryParse(amount, NumberStyles.Number, CultureInfo.InvariantCulture, out var total)
                ? total.ToString("N2", CultureInfo.GetCultureInfo("es-MX")) : amount;
            _page.DrawText($"$ {amountLabel} MXN", Margin, _cursorY - 42f, 21f, PdfFont.Bold, RepBlue);
            _page.DrawTextRight($"{rep.Payments.Count} pago(s) / {rep.Payments.Sum(p => p.Documents.Count)} aplicacion(es)",
                PageWidth - Margin, _cursorY - 36f, 9f, PdfFont.Regular, RepInk);
            _cursorY -= 61f;

            DrawRepSection("Datos del complemento");
            DrawRepGrid(rep.ComplementFields.Where(field => field.Label != "Version Pagos 2.0" && field.Label != "Monto total de pagos").ToArray());
            foreach (var payment in rep.Payments)
            {
                DrawRepSection($"Pago {payment.PaymentNumber}");
                DrawRepGrid(payment.Fields);
                foreach (var document in payment.Documents)
                {
                    EnsureSpace(EstimateRepGridHeight(document.Fields) + 29f);
                    DrawRepSection($"Pago {payment.PaymentNumber} - Documento relacionado {document.DocumentNumber}", small: true);
                    DrawRepGrid(document.Fields);
                    DrawRepTaxes($"Pago {payment.PaymentNumber} - Documento relacionado {document.DocumentNumber} - Impuestos", document.Taxes);
                }
                DrawRepTaxes($"Pago {payment.PaymentNumber} - Impuestos", payment.Taxes);
            }

            DrawRepSection("Datos del CFDI de pago");
            DrawRepParagraph($"Tipo: P - Pago / CFDI {model.CfdiVersion} | Moneda: {model.Currency} | Exportacion: {model.ExportCode}");
            DrawRepParagraph($"Subtotal: {model.Subtotal} | Descuento: {model.Discount} | Total CFDI: {model.Total}. El monto recibido se detalla en Pagos 2.0.");
            foreach (var concept in model.Concepts)
            {
                DrawRepParagraph($"Concepto: {concept.Description} | Clave producto/servicio: {concept.ProductCode} | Cantidad: {concept.Quantity} | Clave unidad: {concept.UnitCode} | Objeto impuesto: {concept.TaxObject} | Valor unitario: {concept.UnitPrice} | Importe: {concept.Amount}");
            }
            DrawRepSection("Certificacion y verificacion SAT");
            DrawRepCertification(model, qr);
            DrawFooter(model);
            return PdfPageBuilder.BuildDocument(_pages);
        }

        private void DrawRepParties(PdfViewModel model)
        {
            var width = (RepWidth - 24f) / 2f;
            var parties = new[]
            {
                new[] { "EMISOR", model.IssuerName, $"RFC: {model.IssuerRfc}", $"Regimen fiscal: {model.IssuerRegime}", $"Lugar de expedicion: {model.PlaceOfIssue}" },
                new[] { "RECEPTOR", model.ReceiverName, $"RFC: {model.ReceiverRfc}", $"Regimen fiscal: {model.ReceiverRegime}", $"Domicilio fiscal: {model.ReceiverPostalCode}", $"Uso CFDI: {model.ReceiverUse}" }
            };
            var columns = parties.Select(party => party.SelectMany((value, index) =>
                WrapRepText(value, width, index == 0 ? 10f : 9f, isBold: index < 2)
                    .Select(line => (Text: line, Bold: index < 2))).ToArray()).ToArray();
            var height = columns.Max(lines => lines.Length) * 12f + 16f;
            EnsureSpace(height);
            for (var column = 0; column < columns.Length; column++)
            {
                var x = Margin + column * (width + 24f);
                _page.DrawLine(x, _cursorY, x + width, _cursorY, RepBlue, 0.8f);
                for (var line = 0; line < columns[column].Length; line++)
                {
                    var value = columns[column][line];
                    _page.DrawText(value.Text, x, _cursorY - 16f - line * 12f, line == 0 ? 10f : 9f,
                        value.Bold ? PdfFont.Bold : PdfFont.Regular, line == 0 ? RepBlue : RepInk);
                }
            }
            _cursorY -= height;
        }

        private void DrawRepParagraph(string text)
        {
            foreach (var line in WrapRepText(text, RepWidth, 8f))
            {
                EnsureSpace(14f);
                _page.DrawText(line, Margin, _cursorY - 8f, 8f, PdfFont.Regular, RepInk);
                _cursorY -= 11f;
            }
            _cursorY -= 5f;
        }

        private float EstimateRepGridHeight(IReadOnlyList<RepField> fields)
        {
            // A conservative keep-together hint; very long values still use the row pagination below.
            var full = fields.Count(field => field.PreferFullWidth);
            var rows = full + (int)Math.Ceiling((fields.Count - full) / 3d);
            return Math.Min(360f, rows * 39f + 4f);
        }

        private void DrawRepHeading(PdfViewModel model, PdfImageAsset? logo)
        {
            var titleX = Margin;
            if (logo is not null)
            {
                var fitted = Fit(logo.Width, logo.Height, 62f, 43f);
                _page.DrawImage(logo, Margin, _cursorY - 44f, fitted.Width, fitted.Height);
                titleX += 76f;
            }
            _page.DrawText("COMPLEMENTO DE PAGO", titleX, _cursorY - 17f, 20f, PdfFont.Bold, RepBlue);
            _page.DrawText("Recibo electronico de pagos / Pagos 2.0", titleX, _cursorY - 35f, 10f, PdfFont.Regular, RepGold);
            _cursorY -= 52f;
            DrawRepStatus(model);
            DrawRepGrid([
                new("Folio", model.SeriesFolio), new("Fecha de emision", model.IssuedAt),
                new("Fecha de timbrado", model.StampedAt), new("Folio fiscal / UUID", model.Uuid, true)
            ]);
        }

        private void DrawRepStatus(PdfViewModel model)
        {
            var status = model.Status switch
            {
                Domain.Enums.FiscalDocumentStatus.Cancelled => "CANCELADO - evidencia historica",
                Domain.Enums.FiscalDocumentStatus.CancellationRequested => "CANCELACION SOLICITADA - pendiente de resolucion",
                _ => "TIMBRADO - evidencia del XML certificado"
            };
            var color = model.Status is Domain.Enums.FiscalDocumentStatus.Cancelled or Domain.Enums.FiscalDocumentStatus.CancellationRequested
                ? new PdfColor(146, 38, 38) : RepGold;
            _page.DrawText(status, Margin, _cursorY - 10f, 9f, PdfFont.Bold, color);
            _cursorY -= 24f;
        }

        private void DrawRepContinuationHeader(PdfViewModel model)
        {
            _page.DrawText("COMPLEMENTO DE PAGO / CONTINUACION", Margin, _cursorY - 12f, 11f, PdfFont.Bold, RepBlue);
            _page.DrawText($"UUID: {model.Uuid}", Margin, _cursorY - 27f, 8f, PdfFont.Regular, RepInk);
            _cursorY -= 37f;
            DrawRepStatus(model);
        }

        private void DrawRepSection(string title, bool small = false)
        {
            EnsureSpace(65f);
            _page.DrawLine(Margin, _cursorY, PageWidth - Margin, _cursorY, small ? RepRule : RepBlue, small ? 0.5f : 0.8f);
            _page.DrawText(title, Margin, _cursorY - 16f, small ? 9f : 10.5f, PdfFont.Bold, small ? RepGold : RepBlue);
            _cursorY -= 29f;
        }

        // Rows split across pages, including unusually long optional bank certificates / payment chains.
        // Values stay as stamped: no timezone conversion, currency mixing, balance recomputation or truncation.
        private void DrawRepGrid(IReadOnlyList<RepField> fields)
        {
            var row = new List<RepField>(3);
            foreach (var field in fields)
            {
                if (field.PreferFullWidth)
                {
                    if (row.Count > 0) { DrawRepGridRow(row); row.Clear(); }
                    DrawRepGridRow([field]);
                }
                else
                {
                    row.Add(field);
                    if (row.Count == 3) { DrawRepGridRow(row); row.Clear(); }
                }
            }
            if (row.Count > 0) DrawRepGridRow(row);
            _cursorY -= 4f;
        }

        private void DrawRepGridRow(IReadOnlyList<RepField> row)
        {
            var width = row.Count == 1 && row[0].PreferFullWidth ? RepWidth : (RepWidth - 24f) / 3f;
            var values = row.Select(field => WrapRepText(field.Value, width, 9f)).ToArray();
            var labels = row.Select(field => WrapRepText(field.Label, width, 8f, isBold: true)).ToArray();
            var labelHeight = labels.Max(lines => lines.Length) * 10f;
            var maxLines = values.Max(lines => lines.Length);
            var offset = 0;
            do
            {
                EnsureSpace(labelHeight + 30f);
                var fit = Math.Max(1, (int)((_cursorY - ContentBottom - labelHeight - 8f) / 11f));
                var take = Math.Min(Math.Max(1, maxLines - offset), fit);
                for (var column = 0; column < row.Count; column++)
                {
                    var x = Margin + column * (width + 12f);
                    DrawWrappedTextLines(labels[column], x, _cursorY - 8f, 8f, 10f, PdfFont.Bold, RepGold);
                    DrawWrappedTextLines(values[column].Skip(offset).Take(take).ToArray(), x, _cursorY - labelHeight - 10f,
                        9f, 11f, PdfFont.Regular, RepInk);
                }
                _cursorY -= labelHeight + take * 11f + 8f;
                offset += take;
                if (offset < maxLines) StartNewPage();
            } while (offset < maxLines);
        }

        private void DrawRepTaxes(string title, IReadOnlyList<RepTaxRow> taxes)
        {
            if (taxes.Count == 0) return;
            var widths = new[] { 70f, 105f, 60f, 70f, 100f, RepWidth - 405f };
            void Header()
            {
                DrawRepSection(title, small: true);
                var labels = new[] { "Tipo", "Base", "Impuesto", "Factor", "Tasa/Cuota", "Importe" };
                var x = Margin;
                for (var i = 0; i < labels.Length; i++)
                {
                    _page.DrawText(labels[i], x + 2f, _cursorY - 8f, 8f, PdfFont.Bold, RepBlue);
                    x += widths[i];
                }
                _cursorY -= 21f;
            }
            Header();
            foreach (var tax in taxes)
            {
                var cells = new[] { tax.RowType, tax.Base, tax.Tax, tax.FactorType, tax.Rate, tax.Amount }
                    .Select((text, i) => WrapRepText(text, widths[i] - 5f, 8f)).ToArray();
                var height = cells.Max(lines => lines.Length) * 10f + 10f;
                if (!HasSpace(height)) { StartNewPage(); Header(); }
                var x = Margin;
                for (var i = 0; i < cells.Length; i++)
                {
                    DrawWrappedTextLines(cells[i], x + 2f, _cursorY - 8f, 8f, 10f, PdfFont.Regular, RepInk);
                    x += widths[i];
                }
                _cursorY -= height;
                _page.DrawLine(Margin, _cursorY + 3f, PageWidth - Margin, _cursorY + 3f, RepRule, 0.4f);
            }
            _cursorY -= 7f;
        }

        private void DrawRepCertification(PdfViewModel model, PdfImageAsset? qr)
        {
            EnsureSpace(105f);
            var x = Margin;
            var top = _cursorY;
            if (qr is not null)
            {
                _page.DrawImage(qr, Margin, top - 92f, 92f, 92f);
                x += 106f;
            }
            var y = top - 10f;
            foreach (var (label, value) in new[] { ("No. serie certificado SAT", model.SatCertificate), ("No. serie CSD emisor", model.IssuerCertificate) })
            {
                _page.DrawText(label.ToUpperInvariant(), x, y, 8f, PdfFont.Bold, RepGold);
                _page.DrawText(value ?? "N/D", x, y - 13f, 9f, PdfFont.Regular, RepInk);
                y -= 32f;
            }
            _page.DrawText("Escanea el QR para consultar el CFDI en el SAT", x, y - 8f, 8f, PdfFont.Regular, RepInk);
            _cursorY = top - 103f;
            DrawRepCertificateText("Consulta SAT / QR", model.QrPayload);
            DrawRepCertificateText("SELLO DIGITAL DEL SAT", model.SatSeal);
            DrawRepCertificateText("SELLO DIGITAL DEL CFDI", model.CfdiSeal);
            DrawRepCertificateText("CADENA ORIGINAL DEL COMPLEMENTO DE CERTIFICACION DIGITAL DEL SAT", model.OriginalString);
        }

        private void DrawRepCertificateText(string label, string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            var lines = WrapRepText(value, RepWidth, 7.5f);
            EnsureSpace(30f);
            _page.DrawText(label.ToUpperInvariant(), Margin, _cursorY - 9f, 8f, PdfFont.Bold, RepGold);
            _cursorY -= 22f;
            foreach (var line in lines)
            {
                if (!HasSpace(13f))
                {
                    StartNewPage();
                    _page.DrawText(label.ToUpperInvariant(), Margin, _cursorY - 9f, 8f, PdfFont.Bold, RepGold);
                    _cursorY -= 22f;
                }
                _page.DrawText(line, Margin, _cursorY, 7.5f, PdfFont.Regular, RepInk);
                _cursorY -= 10f;
            }
            _cursorY -= 7f;
        }

        // Standard Helvetica glyph advances (1/1000 em), matching the fonts written by PdfPageBuilder.
        // Measuring actual glyphs prevents long seals and uppercase names from crossing the page edge.
        private static readonly int[] RepRegularWidths = [278, 278, 355, 556, 556, 889, 667, 191, 333, 333, 389, 584, 278, 333, 278, 278, 556, 556, 556, 556, 556, 556, 556, 556, 556, 556, 278, 278, 584, 584, 584, 556, 1015, 667, 667, 722, 722, 667, 611, 778, 722, 278, 500, 667, 556, 833, 722, 778, 667, 778, 722, 667, 611, 722, 667, 944, 667, 667, 611, 278, 278, 278, 469, 556, 333, 556, 556, 500, 556, 556, 278, 556, 556, 222, 222, 500, 222, 833, 556, 556, 556, 556, 333, 500, 278, 556, 500, 722, 500, 500, 500, 334, 260, 334, 584];
        private static readonly int[] RepBoldWidths = [278, 333, 474, 556, 556, 889, 722, 238, 333, 333, 389, 584, 278, 333, 278, 278, 556, 556, 556, 556, 556, 556, 556, 556, 556, 556, 333, 333, 584, 584, 584, 611, 975, 722, 722, 722, 722, 667, 611, 778, 722, 278, 556, 722, 611, 833, 722, 778, 667, 778, 722, 667, 611, 722, 667, 944, 667, 667, 611, 333, 278, 333, 584, 556, 333, 556, 611, 556, 611, 556, 333, 611, 611, 278, 278, 556, 278, 889, 611, 611, 611, 611, 389, 556, 333, 611, 556, 778, 556, 556, 500, 389, 280, 389, 584];
        private static string[] WrapRepText(string? value, float width, float fontSize, bool isBold = false)
        {
            var remaining = NormalizePdfText(value ?? string.Empty).Trim();
            if (remaining.Length == 0) return [string.Empty];
            var metrics = isBold ? RepBoldWidths : RepRegularWidths;
            var lines = new List<string>();
            while (remaining.Length > 0)
            {
                var measured = 0f;
                var count = 0;
                var lastSpace = -1;
                while (count < remaining.Length)
                {
                    var ch = remaining[count];
                    var advance = metrics[Math.Clamp(ch - 32, 0, 94)] * fontSize / 1000f;
                    if (count > 0 && measured + advance > width - 2f) break;
                    measured += advance;
                    if (ch == ' ') lastSpace = count;
                    count++;
                }
                if (count < remaining.Length && lastSpace > 0) count = lastSpace;
                lines.Add(remaining[..count].TrimEnd());
                remaining = remaining[count..].TrimStart();
            }
            return lines.ToArray();
        }

        private void DrawRepFooter(PdfViewModel model)
        {
            _page.DrawLine(Margin, 51f, PageWidth - Margin, 51f, RepRule, 0.5f);
            _page.DrawText("Este documento es una representacion impresa de un CFDI", Margin, 39f, 7.5f, PdfFont.Regular, RepInk);
            _page.DrawText($"UUID: {model.Uuid}", Margin, 27f, 7f, PdfFont.Regular, RepInk);
            _page.DrawTextRight($"Pagina {_pages.Count}", PageWidth - Margin, 27f, 7.5f, PdfFont.Bold, RepBlue);
        }
    }
}
