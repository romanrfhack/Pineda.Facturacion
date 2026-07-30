using System.Globalization;
using System.Text;
using Pineda.Facturacion.Application.Abstractions.Documents;
using Pineda.Facturacion.Application.UseCases.Orders;

namespace Pineda.Facturacion.Infrastructure.Documents;

public sealed class OrderDebtSummaryPdfRenderer : IOrderDebtSummaryPdfRenderer
{
    public Task<byte[]> RenderAsync(
        OrderDebtSummaryDocument document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(PrintableDebtSummaryPdf.Create(document));
    }

    private sealed class PrintableDebtSummaryPdf
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
        private static readonly PdfColor LightLine = new(205, 214, 222);

        private readonly OrderDebtSummaryDocument _document;
        private readonly List<PdfPage> _pages = [];
        private PdfPage _page;
        private float _cursorY;

        private PrintableDebtSummaryPdf(OrderDebtSummaryDocument document)
        {
            _document = document;
            _page = new PdfPage(PageWidth, PageHeight);
            _pages.Add(_page);
            _cursorY = PageHeight - Margin;
        }

        public static byte[] Create(OrderDebtSummaryDocument document)
        {
            return new PrintableDebtSummaryPdf(document).Build();
        }

        private byte[] Build()
        {
            DrawDocumentHeader();
            DrawTextSection("Mensaje", _document.Message);

            if (_document.Options.IncludeTotals && _document.Selection.TotalsByCurrency.Count > 1)
            {
                DrawTotalsTable();
            }

            if (_document.Options.IncludeOrderTable)
            {
                DrawOrdersTable();
            }

            if (_document.Options.IncludePaymentInstructions)
            {
                DrawTextSection(
                    "Instrucciones y seguimiento",
                    "Agradecemos responder indicando cuáles órdenes o notas desean facturar y cualquier " +
                    "aclaración pendiente sobre pago o datos fiscales.");
            }

            if (_document.Options.IncludeIssuerData || _document.Options.IncludeReceiverFiscalData)
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
            const float horizontalPadding = 12f;
            const float orderMetricWidth = 80f;
            const float totalMetricWidth = 152f;
            const float metricGap = 8f;
            const float metricHeight = 36f;

            var recipientText =
                $"Emitido para {_document.Receiver.LegalName} · " +
                OrderDebtSummaryComposer.FormatGeneratedAt(_document.GeneratedAtUtc);
            var recipientLines = WrapText(recipientText, ContentWidth - 24f, 9.5f);
            var headerHeight = 106f + ((Math.Max(1, recipientLines.Count) - 1) * 11f);
            var topY = _cursorY;
            var bottomY = topY - headerHeight;

            _page.StrokeRectangle(Margin, bottomY, ContentWidth, headerHeight, Blue, 1.2f);
            _page.DrawText(
                _document.Issuer.LegalName.ToUpperInvariant(),
                Margin + horizontalPadding,
                topY - 17f,
                9f,
                PdfFont.Bold,
                Accent);

            var currencyText = $"Moneda: {BuildCurrencyLabel(_document.Selection.TotalsByCurrency)}";
            _page.DrawTextRight(
                currencyText,
                Margin + ContentWidth - horizontalPadding,
                topY - 17f,
                8.5f,
                PdfFont.Regular,
                Muted);

            _page.DrawText(
                "RESUMEN DE NOTAS PENDIENTES",
                Margin + horizontalPadding,
                topY - 52f,
                14.3f,
                PdfFont.Bold,
                Blue);

            var totalMetricX = Margin + ContentWidth - horizontalPadding - totalMetricWidth;
            var orderMetricX = totalMetricX - metricGap - orderMetricWidth;
            var metricBottomY = topY - 71f;
            DrawMetric(
                orderMetricX,
                metricBottomY,
                orderMetricWidth,
                metricHeight,
                "ÓRDENES",
                _document.Selection.OrderCount.ToString(CultureInfo.InvariantCulture));
            DrawMetric(
                totalMetricX,
                metricBottomY,
                totalMetricWidth,
                metricHeight,
                "TOTAL SELECCIONADO",
                FormatCurrencyTotals(_document.Selection.TotalsByCurrency));

            var recipientBaseline = topY - 91f;
            DrawLines(
                recipientLines,
                Margin + horizontalPadding,
                recipientBaseline,
                9.5f,
                11f,
                PdfFont.Regular,
                Slate);

            _cursorY = bottomY - SectionGap;
        }

        private void DrawContinuationHeader()
        {
            const float height = 46f;
            var topY = _cursorY;
            var bottomY = topY - height;

            _page.StrokeRectangle(Margin, bottomY, ContentWidth, height, Blue, 1f);
            _page.DrawText(
                "RESUMEN DE NOTAS PENDIENTES · CONTINUACIÓN",
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

        private void DrawMetric(
            float x,
            float y,
            float width,
            float height,
            string label,
            string value)
        {
            _page.StrokeRectangle(x, y, width, height, Blue, 0.8f);
            _page.DrawText(label, x + 8f, y + 23f, 6.4f, PdfFont.Bold, Accent);
            _page.DrawText(
                value,
                x + 8f,
                y + 8f,
                FitFontSize(value, width - 16f, 10.5f, 5.5f, PdfFont.Bold),
                PdfFont.Bold,
                Blue);
        }

        private void DrawTextSection(string title, string text)
        {
            var allLines = WrapText(text, ContentWidth - 24f, 9.5f);
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

                _page.StrokeRectangle(Margin, bottomY, ContentWidth, sectionHeight, Blue, 0.8f);
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
                    9.5f,
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

        private void DrawTotalsTable()
        {
            const float headerRowHeight = 22f;
            const float dataRowHeight = 22f;
            var totals = _document.Selection.TotalsByCurrency;
            var sectionHeight = SectionHeaderHeight + headerRowHeight + (totals.Count * dataRowHeight);
            EnsureSpace(sectionHeight + SectionGap);

            var topY = _cursorY;
            var bottomY = topY - sectionHeight;
            var columns = new[] { 100f, 100f, ContentWidth - 200f };
            var tableTopY = topY - SectionHeaderHeight;

            _page.StrokeRectangle(Margin, bottomY, ContentWidth, sectionHeight, Blue, 0.8f);
            _page.DrawText(
                "TOTALES POR MONEDA",
                Margin + 12f,
                topY - 18f,
                9.5f,
                PdfFont.Bold,
                Accent);
            _page.DrawLine(Margin, tableTopY, Margin + ContentWidth, tableTopY, LightLine, 0.6f);

            DrawTableHeader(
                tableTopY,
                headerRowHeight,
                columns,
                ["MONEDA", "ÓRDENES", "TOTAL"]);

            var rowTopY = tableTopY - headerRowHeight;
            foreach (var total in totals)
            {
                var baseline = rowTopY - 14f;
                _page.DrawText(total.CurrencyCode, Margin + 6f, baseline, 8.5f, PdfFont.Regular, Slate);
                _page.DrawText(
                    total.OrderCount.ToString(CultureInfo.InvariantCulture),
                    Margin + columns[0] + 6f,
                    baseline,
                    8.5f,
                    PdfFont.Regular,
                    Slate);
                _page.DrawTextRight(
                    OrderDebtSummaryComposer.FormatMoney(total.Total, total.CurrencyCode),
                    Margin + ContentWidth - 6f,
                    baseline,
                    8.5f,
                    PdfFont.Bold,
                    Blue);
                rowTopY -= dataRowHeight;
                _page.DrawLine(Margin, rowTopY, Margin + ContentWidth, rowTopY, LightLine, 0.45f);
            }

            DrawColumnLines(tableTopY, bottomY, columns);
            _cursorY = bottomY - SectionGap;
        }

        private void DrawOrdersTable()
        {
            var includeStatus = _document.Options.IncludeBillingStatus;
            var columns = includeStatus
                ? new[] { 68f, 58f, 169f, 45f, 78f, 114f }
                : new[] { 82f, 65f, 225f, 50f, 110f };
            var labels = includeStatus
                ? new[] { "ORDEN / NOTA", "FECHA", "CLIENTE / REFERENCIA", "MONEDA", "TOTAL", "ESTADO" }
                : new[] { "ORDEN / NOTA", "FECHA", "CLIENTE / REFERENCIA", "MONEDA", "TOTAL" };

            var rowIndex = 0;
            var continuation = false;

            while (rowIndex < _document.Orders.Count)
            {
                var firstRow = BuildOrderRow(_document.Orders[rowIndex], columns, includeStatus);
                EnsureSpace(SectionHeaderHeight + 24f + firstRow.Height);
                var segmentTopY = _cursorY;
                var tableTopY = DrawOrdersTableSegmentHeader(segmentTopY, columns, labels, continuation);
                var segmentBottomY = tableTopY - 24f;
                _cursorY = segmentBottomY;
                var rowsOnSegment = 0;

                while (rowIndex < _document.Orders.Count)
                {
                    var row = BuildOrderRow(_document.Orders[rowIndex], columns, includeStatus);
                    if (!HasSpace(row.Height) && rowsOnSegment > 0)
                    {
                        break;
                    }

                    DrawOrderRow(row, columns);
                    rowIndex++;
                    rowsOnSegment++;
                }

                segmentBottomY = _cursorY;
                CloseOrdersTableSegment(segmentTopY, segmentBottomY, columns);
                _cursorY = segmentBottomY - SectionGap;

                if (rowIndex < _document.Orders.Count)
                {
                    continuation = true;
                    StartNewPage();
                }
            }

            if (_document.Orders.Count == 0)
            {
                DrawTextSection("Órdenes / notas incluidas", "No se incluyeron órdenes o notas.");
            }
        }

        private float DrawOrdersTableSegmentHeader(
            float segmentTopY,
            IReadOnlyList<float> columns,
            IReadOnlyList<string> labels,
            bool continuation)
        {
            _page.DrawText(
                continuation
                    ? "ÓRDENES / NOTAS INCLUIDAS · CONTINUACIÓN"
                    : "ÓRDENES / NOTAS INCLUIDAS",
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

        private OrderTableRow BuildOrderRow(
            OrderDebtSummaryOrder order,
            IReadOnlyList<float> columns,
            bool includeStatus)
        {
            var values = new List<string>
            {
                BuildOrderLabel(order),
                OrderDebtSummaryComposer.FormatDate(order.OrderDateUtc),
                order.CustomerName,
                order.CurrencyCode,
                OrderDebtSummaryComposer.FormatMoney(order.Total, order.CurrencyCode)
            };

            if (includeStatus)
            {
                values.Add(order.BillingStatusLabel);
            }

            var cells = values
                .Select((value, index) => WrapText(value, columns[index] - 10f, 7.6f))
                .ToArray();
            var lineCount = Math.Max(1, cells.Max(cell => cell.Count));
            var height = Math.Max(22f, 9f + (lineCount * 9.4f));
            return new OrderTableRow(cells, height);
        }

        private void DrawOrderRow(OrderTableRow row, IReadOnlyList<float> columns)
        {
            var topY = _cursorY;
            var bottomY = topY - row.Height;
            var x = Margin;

            for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
            {
                var lines = row.Cells[columnIndex];
                var isTotalColumn = columnIndex == 4;
                for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
                {
                    var baseline = topY - 11f - (lineIndex * 9.4f);
                    if (isTotalColumn)
                    {
                        _page.DrawTextRight(
                            lines[lineIndex],
                            x + columns[columnIndex] - 5f,
                            baseline,
                            7.6f,
                            PdfFont.Regular,
                            Slate);
                    }
                    else
                    {
                        _page.DrawText(
                            lines[lineIndex],
                            x + 5f,
                            baseline,
                            7.6f,
                            PdfFont.Regular,
                            Slate);
                    }
                }

                x += columns[columnIndex];
            }

            _page.DrawLine(Margin, bottomY, Margin + ContentWidth, bottomY, LightLine, 0.45f);
            _cursorY = bottomY;
        }

        private void CloseOrdersTableSegment(
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
            var x = Margin;
            for (var index = 0; index < columns.Count; index++)
            {
                _page.DrawText(
                    labels[index],
                    x + 5f,
                    topY - 15f,
                    7.1f,
                    PdfFont.Bold,
                    Blue);
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
            if (_document.Options.IncludeIssuerData)
            {
                blocks.Add(new PartyBlock(
                    "EMISOR",
                    WrapText(FormatParty(_document.Issuer), ContentWidth - 88f, 8.5f)));
            }

            if (_document.Options.IncludeReceiverFiscalData)
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
            _page.StrokeRectangle(Margin, bottomY, ContentWidth, sectionHeight, Blue, 0.8f);
            _page.DrawText(
                "DATOS FISCALES",
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

            var blockTopY = topY - SectionHeaderHeight;
            for (var index = 0; index < blocks.Count; index++)
            {
                var block = blocks[index];
                _page.DrawText(
                    block.Label,
                    Margin + 12f,
                    blockTopY - 16f,
                    8f,
                    PdfFont.Bold,
                    Blue);
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

        private void DrawFooters()
        {
            for (var index = 0; index < _pages.Count; index++)
            {
                var page = _pages[index];
                page.DrawLine(Margin, 36f, Margin + ContentWidth, 36f, Blue, 0.6f);
                page.DrawText(
                    "Resumen informativo generado con base en las órdenes/notas seleccionadas.",
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
                _page.DrawText(
                    lines[index],
                    x,
                    firstBaseline - (index * lineHeight),
                    fontSize,
                    font,
                    color);
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
                    " · ",
                    totals.Select(total => OrderDebtSummaryComposer.FormatMoney(total.Total, total.CurrencyCode)));
        }

        private static string FormatParty(OrderDebtSummaryParty party)
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

        private sealed record OrderTableRow(IReadOnlyList<IReadOnlyList<string>> Cells, float Height);
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
            DrawText(
                text,
                rightX - EstimateTextWidth(text, fontSize, font),
                y,
                fontSize,
                font,
                color);
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
            WriteAscii(
                stream,
                $"<< /Length {content.Length.ToString(CultureInfo.InvariantCulture)} >>\nstream\n");
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
            WriteAscii(
                stream,
                $"xref\n0 {(maxObjectId + 1).ToString(CultureInfo.InvariantCulture)}\n");
            WriteAscii(stream, "0000000000 65535 f \n");

            for (var id = 1; id <= maxObjectId; id++)
            {
                var offset = offsets.TryGetValue(id, out var value) ? value : 0;
                WriteAscii(
                    stream,
                    $"{offset.ToString("0000000000", CultureInfo.InvariantCulture)} 00000 n \n");
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
        public string Command =>
            $"{Fmt(Red / 255f)} {Fmt(Green / 255f)} {Fmt(Blue / 255f)}";
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
            .Replace('\u00a0', ' ');
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
