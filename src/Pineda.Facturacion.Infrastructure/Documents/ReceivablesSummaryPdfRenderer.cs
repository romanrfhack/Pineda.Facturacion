using System.Globalization;
using System.Text;
using Pineda.Facturacion.Application.Abstractions.Documents;
using Pineda.Facturacion.Application.UseCases.AccountsReceivable;

namespace Pineda.Facturacion.Infrastructure.Documents;

public sealed class ReceivablesSummaryPdfRenderer : IReceivablesSummaryPdfRenderer
{
    public Task<byte[]> RenderAsync(
        ReceivablesSummaryDocument document,
        ReceivablesSummaryPdfVariant variant,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(ReceivablesSummaryPdf.Create(document, variant));
    }

    private sealed class ReceivablesSummaryPdf
    {
        private const float PageWidth = 612f;
        private const float PageHeight = 792f;
        private const float Margin = 40f;
        private const float ContentWidth = PageWidth - (Margin * 2f);
        private const float ContentBottom = 52f;
        private const float SectionGap = 10f;
        private const float SectionHeaderHeight = 28f;

        private static readonly PdfColor Blue = new(24, 37, 51);
        private static readonly PdfColor Accent = new(138, 106, 50);
        private static readonly PdfColor Slate = new(68, 80, 92);
        private static readonly PdfColor Muted = new(103, 113, 124);
        private static readonly PdfColor White = new(255, 255, 255);
        private static readonly PdfColor WarmWhite = new(255, 253, 248);
        private static readonly PdfColor LightBeige = new(239, 231, 216);
        private static readonly PdfColor LightRed = new(255, 244, 241);
        private static readonly PdfColor LightLine = new(205, 214, 222);

        private readonly ReceivablesSummaryDocument _document;
        private readonly ReceivablesSummaryPdfVariant _variant;
        private readonly List<PdfPage> _pages = [];
        private PdfPage _page;
        private float _cursorY;

        private ReceivablesSummaryPdf(
            ReceivablesSummaryDocument document,
            ReceivablesSummaryPdfVariant variant)
        {
            _document = document;
            _variant = variant;
            _page = new PdfPage(PageWidth, PageHeight);
            _pages.Add(_page);
            _cursorY = PageHeight - Margin;
        }

        private bool IsPrint => _variant == ReceivablesSummaryPdfVariant.Print;

        public static byte[] Create(
            ReceivablesSummaryDocument document,
            ReceivablesSummaryPdfVariant variant)
        {
            return new ReceivablesSummaryPdf(document, variant).Build();
        }

        private byte[] Build()
        {
            DrawDocumentHeader();
            DrawTextSection("Mensaje", _document.Message);
            DrawSummaryMetrics();

            if (_document.IncludeOptions.TotalsByCurrency && _document.Selection.TotalsByCurrency.Count > 1)
            {
                DrawTotalsTable();
            }

            if (_document.IncludeOptions.InvoiceTable)
            {
                DrawInvoicesTable();
            }

            if (_document.IncludeOptions.PaymentInstructions)
            {
                DrawTextSection(
                    "Instrucciones de pago",
                    "Favor de realizar el pago conforme a los acuerdos comerciales vigentes y compartir " +
                    "el comprobante para su conciliación. Si requiere una aclaración, indique los folios involucrados.");
            }

            if (_document.IncludeOptions.IssuerData || _document.IncludeOptions.ReceiverFiscalData)
            {
                DrawFiscalData();
            }

            DrawFooters();
            return PdfDocumentWriter.Create(_pages);
        }

        private bool HasSpace(float requiredHeight)
        {
            return (_cursorY - requiredHeight) >= ContentBottom;
        }

        private void EnsureSpace(float requiredHeight)
        {
            if (!HasSpace(requiredHeight))
            {
                StartNewPage();
            }
        }

        private void StartNewPage()
        {
            _page = new PdfPage(PageWidth, PageHeight);
            _pages.Add(_page);
            _cursorY = PageHeight - Margin;
            DrawContinuationHeader();
        }

        private void DrawDocumentHeader()
        {
            const float horizontalPadding = 14f;
            const float headerHeight = 104f;
            var topY = _cursorY;
            var bottomY = topY - headerHeight;
            var titleColor = IsPrint ? Blue : White;
            var detailColor = IsPrint ? Slate : White;

            if (IsPrint)
            {
                _page.StrokeRectangle(Margin, bottomY, ContentWidth, headerHeight, Blue, 1.2f);
            }
            else
            {
                _page.FillRectangle(Margin, bottomY, ContentWidth, headerHeight, Blue);
            }

            _page.DrawText(
                _document.Issuer.LegalName.ToUpperInvariant(),
                Margin + horizontalPadding,
                topY - 19f,
                8.8f,
                PdfFont.Bold,
                IsPrint ? Accent : new PdfColor(216, 199, 160));
            _page.DrawText(
                "RESUMEN DE ADEUDOS PENDIENTES",
                Margin + horizontalPadding,
                topY - 49f,
                17f,
                PdfFont.Bold,
                titleColor);

            var recipientText = $"Emitido para {_document.Receiver.LegalName}";
            var recipientLines = WrapText(recipientText, ContentWidth - 28f, 9.3f);
            DrawLines(
                recipientLines.Take(2).ToArray(),
                Margin + horizontalPadding,
                topY - 69f,
                9.3f,
                10.5f,
                PdfFont.Regular,
                detailColor);

            _page.DrawTextRight(
                FormatGeneratedAt(_document.GeneratedAtUtc),
                Margin + ContentWidth - horizontalPadding,
                bottomY + 10f,
                7.4f,
                PdfFont.Regular,
                IsPrint ? Muted : new PdfColor(216, 225, 233));

            _cursorY = bottomY - SectionGap;
        }

        private void DrawContinuationHeader()
        {
            const float height = 46f;
            var topY = _cursorY;
            var bottomY = topY - height;

            _page.StrokeRectangle(Margin, bottomY, ContentWidth, height, Blue, 1f);
            _page.DrawText(
                "RESUMEN DE ADEUDOS · CONTINUACIÓN",
                Margin + 12f,
                topY - 18f,
                11.5f,
                PdfFont.Bold,
                Blue);
            _page.DrawText(
                $"Receptor: {_document.Receiver.LegalName}",
                Margin + 12f,
                topY - 34f,
                8.5f,
                PdfFont.Regular,
                Slate);

            _cursorY = bottomY - SectionGap;
        }

        private void DrawTextSection(string title, string text)
        {
            var allLines = WrapText(text, ContentWidth - 24f, 9.3f);
            var nextLine = 0;

            do
            {
                EnsureSpace(52f);
                var availableForLines = _cursorY - ContentBottom - SectionHeaderHeight - 14f;
                var maxLines = Math.Max(1, (int)Math.Floor(availableForLines / 12f));
                var lineCount = Math.Min(maxLines, Math.Max(1, allLines.Count - nextLine));
                var sectionHeight = SectionHeaderHeight + 10f + (lineCount * 12f) + 6f;
                var topY = _cursorY;
                var bottomY = topY - sectionHeight;

                DrawSectionFrame(topY, bottomY);
                _page.DrawText(
                    nextLine == 0 ? title.ToUpperInvariant() : $"{title.ToUpperInvariant()} · CONTINUACIÓN",
                    Margin + 12f,
                    topY - 18f,
                    9.5f,
                    PdfFont.Bold,
                    Accent);
                _page.DrawLine(
                    Margin,
                    topY - SectionHeaderHeight,
                    Margin + ContentWidth,
                    topY - SectionHeaderHeight,
                    LightLine,
                    0.6f);

                var chunk = allLines.Skip(nextLine).Take(lineCount).ToArray();
                DrawLines(
                    chunk,
                    Margin + 12f,
                    topY - SectionHeaderHeight - 16f,
                    9.3f,
                    12f,
                    PdfFont.Regular,
                    Slate);

                nextLine += lineCount;
                _cursorY = bottomY - SectionGap;

                if (nextLine < allLines.Count)
                {
                    StartNewPage();
                }
            }
            while (nextLine < allLines.Count);
        }

        private void DrawSummaryMetrics()
        {
            const float metricGap = 6f;
            const float metricHeight = 54f;
            var metricWidth = (ContentWidth - (metricGap * 3f)) / 4f;
            EnsureSpace(metricHeight + SectionGap);

            var values = new[]
            {
                ("FACTURAS", _document.Selection.InvoiceCount.ToString(CultureInfo.InvariantCulture)),
                ("SALDO TOTAL", FormatCurrencyTotals(_document.Selection.TotalsByCurrency, x => x.OutstandingBalance)),
                ("SALDO VENCIDO", FormatCurrencyTotals(_document.Selection.TotalsByCurrency, x => x.OverdueBalance)),
                ("POR VENCER", FormatCurrencyTotals(_document.Selection.TotalsByCurrency, x => x.CurrentBalance))
            };

            var x = Margin;
            foreach (var metric in values)
            {
                if (!IsPrint)
                {
                    _page.FillRectangle(x, _cursorY - metricHeight, metricWidth, metricHeight, WarmWhite);
                }
                _page.StrokeRectangle(x, _cursorY - metricHeight, metricWidth, metricHeight, Blue, 0.75f);
                _page.DrawText(metric.Item1, x + 8f, _cursorY - 17f, 6.7f, PdfFont.Bold, Accent);
                _page.DrawText(
                    metric.Item2,
                    x + 8f,
                    _cursorY - 39f,
                    FitFontSize(metric.Item2, metricWidth - 16f, 10.3f, 5.4f, PdfFont.Bold),
                    PdfFont.Bold,
                    Blue);
                x += metricWidth + metricGap;
            }

            _cursorY -= metricHeight + SectionGap;
        }

        private void DrawTotalsTable()
        {
            const float headerRowHeight = 22f;
            const float dataRowHeight = 22f;
            var totals = _document.Selection.TotalsByCurrency;
            var sectionHeight = SectionHeaderHeight + headerRowHeight + (totals.Count * dataRowHeight);
            EnsureSpace(sectionHeight + SectionGap);

            var topY = _cursorY;
            var bottomY = topY - sectionHeight;
            var columns = new[] { 52f, 45f, 87f, 87f, 87f, 87f, 87f };
            var labels = new[] { "MON.", "FACT.", "TOTAL", "PAGADO", "SALDO", "VENCIDO", "POR VENCER" };
            var tableTopY = topY - SectionHeaderHeight;

            DrawSectionFrame(topY, bottomY);
            _page.DrawText("TOTALES POR MONEDA", Margin + 12f, topY - 18f, 9.5f, PdfFont.Bold, Accent);
            _page.DrawLine(Margin, tableTopY, Margin + ContentWidth, tableTopY, LightLine, 0.6f);
            DrawTableHeader(tableTopY, headerRowHeight, columns, labels);

            var rowTopY = tableTopY - headerRowHeight;
            foreach (var total in totals)
            {
                var values = new[]
                {
                    total.CurrencyCode,
                    total.InvoiceCount.ToString(CultureInfo.InvariantCulture),
                    ReceivablesSummaryComposer.FormatMoney(total.Total, total.CurrencyCode),
                    ReceivablesSummaryComposer.FormatMoney(total.PaidTotal, total.CurrencyCode),
                    ReceivablesSummaryComposer.FormatMoney(total.OutstandingBalance, total.CurrencyCode),
                    ReceivablesSummaryComposer.FormatMoney(total.OverdueBalance, total.CurrencyCode),
                    ReceivablesSummaryComposer.FormatMoney(total.CurrentBalance, total.CurrencyCode)
                };
                var x = Margin;
                for (var index = 0; index < values.Length; index++)
                {
                    var fontSize = FitFontSize(values[index], columns[index] - 10f, 7.1f, 5.2f, PdfFont.Regular);
                    if (index >= 2)
                    {
                        _page.DrawTextRight(values[index], x + columns[index] - 5f, rowTopY - 14f, fontSize, PdfFont.Regular, Slate);
                    }
                    else
                    {
                        _page.DrawText(values[index], x + 5f, rowTopY - 14f, fontSize, PdfFont.Regular, Slate);
                    }
                    x += columns[index];
                }

                rowTopY -= dataRowHeight;
                _page.DrawLine(Margin, rowTopY, Margin + ContentWidth, rowTopY, LightLine, 0.45f);
            }

            DrawColumnLines(tableTopY, bottomY, columns);
            _cursorY = bottomY - SectionGap;
        }

        private void DrawInvoicesTable()
        {
            var columns = new[] { 60f, 50f, 58f, 28f, 38f, 68f, 62f, 68f, 100f };
            var labels = new[] { "FOLIO", "EMISIÓN", "VENCE", "DÍAS", "MON.", "TOTAL", "PAGADO", "SALDO", "ESTADO" };
            var rowIndex = 0;
            var continuation = false;

            while (rowIndex < _document.Invoices.Count)
            {
                var firstRow = BuildInvoiceRow(_document.Invoices[rowIndex], columns);
                EnsureSpace(SectionHeaderHeight + 24f + firstRow.Height);
                var segmentTopY = _cursorY;
                var tableTopY = DrawInvoicesTableSegmentHeader(segmentTopY, columns, labels, continuation);
                _cursorY = tableTopY - 24f;
                var rowsOnSegment = 0;

                while (rowIndex < _document.Invoices.Count)
                {
                    var invoice = _document.Invoices[rowIndex];
                    var row = BuildInvoiceRow(invoice, columns);
                    if (!HasSpace(row.Height) && rowsOnSegment > 0)
                    {
                        break;
                    }

                    DrawInvoiceRow(row, columns, invoice.IsOverdue && _document.IncludeOptions.HighlightOverdue);
                    rowIndex++;
                    rowsOnSegment++;
                }

                var segmentBottomY = _cursorY;
                CloseInvoicesTableSegment(segmentTopY, segmentBottomY, columns);
                _cursorY = segmentBottomY - SectionGap;

                if (rowIndex < _document.Invoices.Count)
                {
                    continuation = true;
                    StartNewPage();
                }
            }

            if (_document.Invoices.Count == 0)
            {
                DrawTextSection("Facturas incluidas", "No se incluyeron facturas.");
            }
        }

        private float DrawInvoicesTableSegmentHeader(
            float segmentTopY,
            IReadOnlyList<float> columns,
            IReadOnlyList<string> labels,
            bool continuation)
        {
            _page.DrawText(
                continuation ? "FACTURAS INCLUIDAS · CONTINUACIÓN" : "FACTURAS INCLUIDAS",
                Margin + 12f,
                segmentTopY - 18f,
                9.5f,
                PdfFont.Bold,
                Accent);

            var tableTopY = segmentTopY - SectionHeaderHeight;
            _page.DrawLine(Margin, tableTopY, Margin + ContentWidth, tableTopY, LightLine, 0.6f);
            DrawTableHeader(tableTopY, 24f, columns, labels);
            return tableTopY;
        }

        private InvoiceTableRow BuildInvoiceRow(
            ReceivablesSummaryCandidate invoice,
            IReadOnlyList<float> columns)
        {
            var values = new[]
            {
                ReceivablesSummaryComposer.FormatInvoiceLabel(invoice),
                ReceivablesSummaryComposer.FormatDate(invoice.IssuedAtUtc),
                ReceivablesSummaryComposer.FormatDate(invoice.DueAtUtc),
                invoice.IsOverdue ? invoice.DaysPastDue.ToString(CultureInfo.InvariantCulture) : "-",
                invoice.CurrencyCode,
                ReceivablesSummaryComposer.FormatMoney(invoice.Total, invoice.CurrencyCode),
                ReceivablesSummaryComposer.FormatMoney(invoice.PaidTotal, invoice.CurrencyCode),
                ReceivablesSummaryComposer.FormatMoney(invoice.OutstandingBalance, invoice.CurrencyCode),
                invoice.Status
            };
            var cells = values
                .Select((value, index) => WrapText(value, columns[index] - 10f, 6.8f))
                .ToArray();
            var lineCount = Math.Max(1, cells.Max(cell => cell.Count));
            var height = Math.Max(21f, 8f + (lineCount * 8.4f));
            return new InvoiceTableRow(cells, height);
        }

        private void DrawInvoiceRow(
            InvoiceTableRow row,
            IReadOnlyList<float> columns,
            bool highlightOverdue)
        {
            var topY = _cursorY;
            var bottomY = topY - row.Height;
            if (highlightOverdue && !IsPrint)
            {
                _page.FillRectangle(Margin, bottomY, ContentWidth, row.Height, LightRed);
            }

            var x = Margin;
            for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
            {
                var lines = row.Cells[columnIndex];
                var isMoneyColumn = columnIndex is 5 or 6 or 7;
                for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
                {
                    var baseline = topY - 10f - (lineIndex * 8.4f);
                    if (isMoneyColumn)
                    {
                        _page.DrawTextRight(
                            lines[lineIndex],
                            x + columns[columnIndex] - 5f,
                            baseline,
                            6.8f,
                            PdfFont.Regular,
                            Slate);
                    }
                    else
                    {
                        _page.DrawText(
                            lines[lineIndex],
                            x + 5f,
                            baseline,
                            6.8f,
                            PdfFont.Regular,
                            Slate);
                    }
                }

                x += columns[columnIndex];
            }

            _page.DrawLine(Margin, bottomY, Margin + ContentWidth, bottomY, LightLine, 0.45f);
            _cursorY = bottomY;
        }

        private void CloseInvoicesTableSegment(
            float segmentTopY,
            float segmentBottomY,
            IReadOnlyList<float> columns)
        {
            _page.StrokeRectangle(
                Margin,
                segmentBottomY,
                ContentWidth,
                segmentTopY - segmentBottomY,
                Blue,
                0.8f);
            DrawColumnLines(segmentTopY - SectionHeaderHeight, segmentBottomY, columns);
        }

        private void DrawTableHeader(
            float topY,
            float height,
            IReadOnlyList<float> columns,
            IReadOnlyList<string> labels)
        {
            if (!IsPrint)
            {
                _page.FillRectangle(Margin, topY - height, ContentWidth, height, LightBeige);
            }

            var x = Margin;
            for (var index = 0; index < columns.Count; index++)
            {
                _page.DrawText(labels[index], x + 5f, topY - 15f, 6.5f, PdfFont.Bold, Blue);
                x += columns[index];
            }

            _page.DrawLine(Margin, topY - height, Margin + ContentWidth, topY - height, LightLine, 0.55f);
        }

        private void DrawColumnLines(
            float tableTopY,
            float tableBottomY,
            IReadOnlyList<float> columns)
        {
            var x = Margin;
            for (var index = 0; index < columns.Count - 1; index++)
            {
                x += columns[index];
                _page.DrawLine(x, tableTopY, x, tableBottomY, LightLine, 0.45f);
            }
        }

        private void DrawFiscalData()
        {
            var blocks = new List<PartyBlock>();
            if (_document.IncludeOptions.IssuerData)
            {
                blocks.Add(new PartyBlock(
                    "EMISOR",
                    WrapText(FormatParty(_document.Issuer), ContentWidth - 88f, 8.5f)));
            }

            if (_document.IncludeOptions.ReceiverFiscalData)
            {
                blocks.Add(new PartyBlock(
                    "RECEPTOR",
                    WrapText(FormatParty(_document.Receiver), ContentWidth - 88f, 8.5f)));
            }

            var blockHeights = blocks
                .Select(block => Math.Max(26f, 10f + (Math.Max(1, block.Lines.Count) * 10f)))
                .ToArray();
            var sectionHeight = SectionHeaderHeight + blockHeights.Sum() + 6f;
            EnsureSpace(sectionHeight + SectionGap);

            var topY = _cursorY;
            var bottomY = topY - sectionHeight;
            DrawSectionFrame(topY, bottomY);
            _page.DrawText("DATOS FISCALES", Margin + 12f, topY - 18f, 9.5f, PdfFont.Bold, Accent);
            _page.DrawLine(
                Margin,
                topY - SectionHeaderHeight,
                Margin + ContentWidth,
                topY - SectionHeaderHeight,
                LightLine,
                0.6f);

            var blockTopY = topY - SectionHeaderHeight;
            for (var index = 0; index < blocks.Count; index++)
            {
                var block = blocks[index];
                _page.DrawText(block.Label, Margin + 12f, blockTopY - 16f, 8f, PdfFont.Bold, Blue);
                DrawLines(
                    block.Lines,
                    Margin + 76f,
                    blockTopY - 16f,
                    8.5f,
                    10f,
                    PdfFont.Regular,
                    Slate);

                blockTopY -= blockHeights[index];
                if (index < blocks.Count - 1)
                {
                    _page.DrawLine(Margin, blockTopY, Margin + ContentWidth, blockTopY, LightLine, 0.45f);
                }
            }

            _cursorY = bottomY - SectionGap;
        }

        private void DrawSectionFrame(float topY, float bottomY)
        {
            if (!IsPrint)
            {
                _page.FillRectangle(Margin, bottomY, ContentWidth, topY - bottomY, White);
            }
            _page.StrokeRectangle(Margin, bottomY, ContentWidth, topY - bottomY, Blue, 0.8f);
        }

        private void DrawFooters()
        {
            for (var index = 0; index < _pages.Count; index++)
            {
                var page = _pages[index];
                page.DrawLine(Margin, 36f, Margin + ContentWidth, 36f, Blue, 0.6f);
                page.DrawText(
                    "Resumen informativo basado en los registros actuales de cuentas por cobrar.",
                    Margin,
                    23f,
                    7.2f,
                    PdfFont.Regular,
                    Muted);
                page.DrawTextRight(
                    $"Página {(index + 1).ToString(CultureInfo.InvariantCulture)} de {_pages.Count.ToString(CultureInfo.InvariantCulture)}",
                    Margin + ContentWidth,
                    23f,
                    7.2f,
                    PdfFont.Regular,
                    Muted);
            }
        }

        private void DrawLines(
            IReadOnlyList<string> lines,
            float x,
            float firstBaseline,
            float fontSize,
            float lineHeight,
            PdfFont font,
            PdfColor color)
        {
            for (var index = 0; index < lines.Count; index++)
            {
                _page.DrawText(lines[index], x, firstBaseline - (index * lineHeight), fontSize, font, color);
            }
        }

        private static string FormatGeneratedAt(DateTime value)
        {
            return value.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
        }

        private static string FormatCurrencyTotals(
            IReadOnlyList<ReceivablesSummaryTotalByCurrency> totals,
            Func<ReceivablesSummaryTotalByCurrency, decimal> selector)
        {
            return totals.Count == 0
                ? ReceivablesSummaryComposer.FormatMoney(0m, "MXN")
                : string.Join(
                    " · ",
                    totals.Select(total => ReceivablesSummaryComposer.FormatMoney(selector(total), total.CurrencyCode)));
        }

        private static string FormatParty(ReceivablesSummaryParty party)
        {
            var values = new List<string> { party.LegalName };
            if (!string.IsNullOrWhiteSpace(party.Rfc))
            {
                values.Add($"RFC: {party.Rfc}");
            }

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

            return string.Join(" · ", values);
        }

        private sealed record InvoiceTableRow(IReadOnlyList<IReadOnlyList<string>> Cells, float Height);
        private sealed record PartyBlock(string Label, IReadOnlyList<string> Lines);
    }

    private sealed class PdfPage
    {
        private readonly StringBuilder _content = new();

        public PdfPage(float width, float height)
        {
            Width = width;
            Height = height;
        }

        public float Width { get; }
        public float Height { get; }
        public string Content => _content.ToString();

        public void FillRectangle(float x, float y, float width, float height, PdfColor color)
        {
            _content.AppendLine($"{color.Command} rg");
            _content.AppendLine($"{Fmt(x)} {Fmt(y)} {Fmt(width)} {Fmt(height)} re f");
        }

        public void StrokeRectangle(
            float x,
            float y,
            float width,
            float height,
            PdfColor color,
            float lineWidth)
        {
            _content.AppendLine($"{Fmt(lineWidth)} w");
            _content.AppendLine($"{color.Command} RG");
            _content.AppendLine($"{Fmt(x)} {Fmt(y)} {Fmt(width)} {Fmt(height)} re S");
        }

        public void DrawLine(
            float x1,
            float y1,
            float x2,
            float y2,
            PdfColor color,
            float lineWidth)
        {
            _content.AppendLine($"{Fmt(lineWidth)} w");
            _content.AppendLine($"{color.Command} RG");
            _content.AppendLine($"{Fmt(x1)} {Fmt(y1)} m {Fmt(x2)} {Fmt(y2)} l S");
        }

        public void DrawText(
            string text,
            float x,
            float y,
            float fontSize,
            PdfFont font,
            PdfColor color)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            _content.AppendLine("BT");
            _content.AppendLine($"/{FontResource(font)} {Fmt(fontSize)} Tf");
            _content.AppendLine($"{color.Command} rg");
            _content.AppendLine($"{Fmt(x)} {Fmt(y)} Td");
            _content.AppendLine($"({EscapePdfLiteral(NormalizePdfText(text))}) Tj");
            _content.AppendLine("ET");
        }

        public void DrawTextRight(
            string text,
            float rightX,
            float y,
            float fontSize,
            PdfFont font,
            PdfColor color)
        {
            DrawText(text, rightX - EstimateTextWidth(text, fontSize, font), y, fontSize, font, color);
        }
    }

    private static class PdfDocumentWriter
    {
        public static byte[] Create(IReadOnlyList<PdfPage> pages)
        {
            var objects = new List<PdfObject>();
            var nextId = 1;
            var catalogId = nextId++;
            var pagesId = nextId++;
            var regularFontId = nextId++;
            var boldFontId = nextId++;

            objects.Add(PdfObject.FromText(catalogId, $"<< /Type /Catalog /Pages {pagesId} 0 R >>"));
            objects.Add(PdfObject.FromText(
                regularFontId,
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"));
            objects.Add(PdfObject.FromText(
                boldFontId,
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>"));

            var pageIds = new List<int>();
            foreach (var page in pages)
            {
                var contentId = nextId++;
                objects.Add(PdfObject.FromStream(contentId, Encoding.Latin1.GetBytes(page.Content)));

                var pageId = nextId++;
                pageIds.Add(pageId);
                objects.Add(PdfObject.FromText(
                    pageId,
                    $"<< /Type /Page /Parent {pagesId} 0 R /MediaBox [0 0 {Fmt(page.Width)} {Fmt(page.Height)}] " +
                    $"/Resources << /Font << /F1 {regularFontId} 0 R /F2 {boldFontId} 0 R >> >> " +
                    $"/Contents {contentId} 0 R >>"));
            }

            objects.Add(PdfObject.FromText(
                pagesId,
                $"<< /Type /Pages /Count {pageIds.Count.ToString(CultureInfo.InvariantCulture)} " +
                $"/Kids [{string.Join(' ', pageIds.Select(id => $"{id} 0 R"))}] >>"));

            return PdfSerializer.Serialize(objects, catalogId);
        }
    }

    private sealed class PdfObject
    {
        public required int Id { get; init; }
        public required byte[] Body { get; init; }

        public static PdfObject FromText(int id, string body)
        {
            return new PdfObject
            {
                Id = id,
                Body = Encoding.ASCII.GetBytes(body)
            };
        }

        public static PdfObject FromStream(int id, byte[] content)
        {
            using var stream = new MemoryStream();
            WriteAscii(stream, $"<< /Length {content.Length.ToString(CultureInfo.InvariantCulture)} >>\nstream\n");
            stream.Write(content);
            WriteAscii(stream, "\nendstream");
            return new PdfObject
            {
                Id = id,
                Body = stream.ToArray()
            };
        }
    }

    private static class PdfSerializer
    {
        public static byte[] Serialize(IReadOnlyList<PdfObject> sourceObjects, int catalogId)
        {
            var objects = sourceObjects.OrderBy(item => item.Id).ToArray();
            using var stream = new MemoryStream();
            WriteAscii(stream, "%PDF-1.4\n");

            var offsets = new Dictionary<int, long>();
            foreach (var item in objects)
            {
                offsets[item.Id] = stream.Position;
                WriteAscii(stream, $"{item.Id.ToString(CultureInfo.InvariantCulture)} 0 obj\n");
                stream.Write(item.Body);
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
                $"trailer\n<< /Size {(maxObjectId + 1).ToString(CultureInfo.InvariantCulture)} " +
                $"/Root {catalogId.ToString(CultureInfo.InvariantCulture)} 0 R >>\n" +
                $"startxref\n{xrefPosition.ToString(CultureInfo.InvariantCulture)}\n%%EOF");
            return stream.ToArray();
        }
    }

    private sealed record PdfColor(byte Red, byte Green, byte Blue)
    {
        public string Command => $"{Fmt(Red / 255f)} {Fmt(Green / 255f)} {Fmt(Blue / 255f)}";
    }

    private enum PdfFont
    {
        Regular,
        Bold
    }

    private static IReadOnlyList<string> WrapText(
        string? value,
        float availableWidth,
        float fontSize,
        bool bold = false)
    {
        var normalized = (value ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        var lines = new List<string>();
        var averageCharacterWidth = fontSize * (bold ? 0.56f : 0.52f);
        var maxCharacters = Math.Max(1, (int)Math.Floor(availableWidth / averageCharacterWidth));

        foreach (var paragraph in normalized.Split('\n'))
        {
            var remaining = paragraph.Trim();
            if (remaining.Length == 0)
            {
                lines.Add(string.Empty);
                continue;
            }

            while (remaining.Length > maxCharacters)
            {
                var splitAt = remaining.LastIndexOf(' ', maxCharacters);
                if (splitAt <= 0)
                {
                    splitAt = maxCharacters;
                }

                lines.Add(remaining[..splitAt].TrimEnd());
                remaining = remaining[splitAt..].TrimStart();
            }

            lines.Add(remaining);
        }

        return lines.Count == 0 ? [string.Empty] : lines;
    }

    private static float EstimateTextWidth(string text, float fontSize, PdfFont font)
    {
        var multiplier = font == PdfFont.Bold ? 0.56f : 0.52f;
        return NormalizePdfText(text).Length * fontSize * multiplier;
    }

    private static float FitFontSize(
        string text,
        float availableWidth,
        float preferredSize,
        float minimumSize,
        PdfFont font)
    {
        var estimatedWidth = EstimateTextWidth(text, preferredSize, font);
        if (estimatedWidth <= availableWidth || estimatedWidth <= 0f)
        {
            return preferredSize;
        }

        return Math.Max(minimumSize, preferredSize * (availableWidth / estimatedWidth));
    }

    private static string FontResource(PdfFont font)
    {
        return font == PdfFont.Bold ? "F2" : "F1";
    }

    private static string NormalizePdfText(string value)
    {
        var sanitized = value
            .Replace('\u2011', '-')
            .Replace('\u2013', '-')
            .Replace('\u2014', '-')
            .Replace('\u00a0', ' ')
            .Replace('\u00b7', '-');
        var builder = new StringBuilder(sanitized.Length);

        foreach (var character in sanitized)
        {
            if (character is >= ' ' and <= '\u00ff')
            {
                builder.Append(character);
            }
            else if (!char.IsControl(character))
            {
                builder.Append('?');
            }
        }

        return builder.ToString();
    }

    private static string EscapePdfLiteral(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
    }

    private static string Fmt(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static void WriteAscii(Stream stream, string value)
    {
        stream.Write(Encoding.ASCII.GetBytes(value));
    }
}
