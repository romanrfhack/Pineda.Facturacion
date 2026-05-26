using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using Pineda.Facturacion.Application.Abstractions.Documents;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Abstractions.Storage;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.Documents;

public sealed class FiscalDocumentPdfRenderer : IFiscalDocumentPdfRenderer
{
    private static readonly Regex RepPaymentFieldCodePattern = new(@"^REP_PAYMENT_(\d+)_FIELD_(.+)$", RegexOptions.Compiled);
    private static readonly Regex RepPaymentTaxCodePattern = new(@"^REP_PAYMENT_(\d+)_TAX_(.+)_(\d+)$", RegexOptions.Compiled);
    private static readonly Regex RepDocumentFieldCodePattern = new(@"^REP_PAYMENT_(\d+)_DOCUMENT_(\d+)_FIELD_(.+)$", RegexOptions.Compiled);
    private static readonly Regex RepDocumentTaxCodePattern = new(@"^REP_PAYMENT_(\d+)_DOCUMENT_(\d+)_TAX_(.+)_(\d+)$", RegexOptions.Compiled);

    private readonly IIssuerProfileRepository _issuerProfileRepository;
    private readonly IIssuerProfileLogoStorage _logoStorage;
    private readonly ISatCatalogDescriptionProvider _satCatalogDescriptionProvider;

    public FiscalDocumentPdfRenderer(
        IIssuerProfileRepository issuerProfileRepository,
        IIssuerProfileLogoStorage logoStorage,
        ISatCatalogDescriptionProvider satCatalogDescriptionProvider)
    {
        _issuerProfileRepository = issuerProfileRepository;
        _logoStorage = logoStorage;
        _satCatalogDescriptionProvider = satCatalogDescriptionProvider;
    }

    public async Task<byte[]> RenderAsync(FiscalDocument fiscalDocument, FiscalStamp fiscalStamp, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fiscalDocument);
        ArgumentNullException.ThrowIfNull(fiscalStamp);

        if (string.IsNullOrWhiteSpace(fiscalStamp.XmlContent))
        {
            throw new InvalidOperationException("Stamped XML is required to build the final CFDI PDF.");
        }

        var document = XDocument.Parse(fiscalStamp.XmlContent, LoadOptions.PreserveWhitespace);
        var model = PdfViewModel.Create(fiscalDocument, fiscalStamp, document, _satCatalogDescriptionProvider);
        var logo = await TryLoadIssuerLogoAsync(fiscalDocument.IssuerProfileId, cancellationToken);
        return FiscalPdfDocument.Create(model, logo, TryBuildQrAsset(model.QrPayload));
    }

    private async Task<PdfImageAsset?> TryLoadIssuerLogoAsync(long issuerProfileId, CancellationToken cancellationToken)
    {
        if (issuerProfileId <= 0)
        {
            return null;
        }

        try
        {
            var issuerProfile = await _issuerProfileRepository.GetByIdAsync(issuerProfileId, cancellationToken);
            if (issuerProfile is null)
            {
                return null;
            }

            if (issuerProfile.LogoData is { Length: > 0 })
            {
                return PdfImageAsset.TryCreate(issuerProfile.LogoData, issuerProfile.LogoContentType ?? "application/octet-stream");
            }

            if (string.IsNullOrWhiteSpace(issuerProfile.LogoStoragePath))
            {
                return null;
            }

            var logo = await _logoStorage.ReadAsync(issuerProfile.LogoStoragePath, cancellationToken);
            if (logo is null || logo.Content.Length == 0)
            {
                return null;
            }

            return PdfImageAsset.TryCreate(logo.Content, issuerProfile.LogoContentType ?? logo.ContentType);
        }
        catch
        {
            return null;
        }
    }

    private static PdfImageAsset? TryBuildQrAsset(string? payload)
    {
        var qr = SimpleQrCodeGenerator.TryEncode(payload ?? string.Empty);
        if (qr is null)
        {
            return null;
        }

        const int quietZoneModules = 4;
        const int pixelsPerModule = 4;
        var imageSize = (qr.Size + (quietZoneModules * 2)) * pixelsPerModule;
        var rgb = new byte[imageSize * imageSize * 3];

        for (var y = 0; y < imageSize; y++)
        {
            for (var x = 0; x < imageSize; x++)
            {
                var moduleX = (x / pixelsPerModule) - quietZoneModules;
                var moduleY = (y / pixelsPerModule) - quietZoneModules;
                var dark = qr.GetModule(moduleX, moduleY);
                var value = dark ? (byte)0 : (byte)255;
                var offset = (y * imageSize + x) * 3;
                rgb[offset] = value;
                rgb[offset + 1] = value;
                rgb[offset + 2] = value;
            }
        }

        return PdfImageAsset.CreateRgb(imageSize, imageSize, rgb);
    }

    private sealed class PdfViewModel
    {
        public required string DocumentTitle { get; init; }
        public required string DocumentType { get; init; }
        public required string DocumentTypeCode { get; init; }
        public required string CfdiVersion { get; init; }
        public required string Uuid { get; init; }
        public required string SeriesFolio { get; init; }
        public required string IssuedAt { get; init; }
        public required string StampedAt { get; init; }
        public required string PaymentMethod { get; init; }
        public required string PaymentForm { get; init; }
        public required string ExportCode { get; init; }
        public required string Currency { get; init; }
        public required string PlaceOfIssue { get; init; }
        public required string IssuerName { get; init; }
        public required string IssuerRfc { get; init; }
        public required string IssuerRegime { get; init; }
        public required string? IssuerCertificate { get; init; }
        public required string ReceiverName { get; init; }
        public required string ReceiverRfc { get; init; }
        public required string ReceiverUse { get; init; }
        public required string ReceiverRegime { get; init; }
        public required string ReceiverPostalCode { get; init; }
        public required string TotalInWords { get; init; }
        public required string Subtotal { get; init; }
        public required string Discount { get; init; }
        public required string Taxes { get; init; }
        public required string Total { get; init; }
        public required string? Qr { get; init; }
        public required string? QrPayload { get; init; }
        public required string? OriginalString { get; init; }
        public required string? SatCertificate { get; init; }
        public required string? CfdiSeal { get; init; }
        public required string? SatSeal { get; init; }
        public required string? ProviderName { get; init; }
        public required IReadOnlyList<PdfConceptRow> Concepts { get; init; }
        public required IReadOnlyList<PdfTaxRow> TaxesBreakdown { get; init; }
        public required IReadOnlyList<PdfAdditionalFieldRow> AdditionalFields { get; init; }

        public static PdfViewModel Create(FiscalDocument fiscalDocument, FiscalStamp fiscalStamp, XDocument document, ISatCatalogDescriptionProvider satCatalogDescriptionProvider)
        {
            var comprobante = document.Descendants().FirstOrDefault(static node => node.Name.LocalName == "Comprobante");
            var emisor = document.Descendants().FirstOrDefault(static node => node.Name.LocalName == "Emisor");
            var receptor = document.Descendants().FirstOrDefault(static node => node.Name.LocalName == "Receptor");
            var timbre = document.Descendants().FirstOrDefault(static node => node.Name.LocalName == "TimbreFiscalDigital");
            var conceptos = document.Descendants().Where(static node => node.Name.LocalName == "Concepto").ToList();
            var total = ParseDecimal(GetAttribute(comprobante, "Total"), fiscalDocument.Total);
            var documentTypeCode = GetAttribute(comprobante, "TipoDeComprobante") ?? fiscalDocument.DocumentType ?? string.Empty;
            var paymentMethodCode = GetAttribute(comprobante, "MetodoPago");
            var paymentFormCode = GetAttribute(comprobante, "FormaPago");

            return new PdfViewModel
            {
                DocumentTitle = "Representacion impresa del CFDI",
                DocumentType = ResolveDocumentType(documentTypeCode),
                DocumentTypeCode = documentTypeCode,
                CfdiVersion = GetAttribute(comprobante, "Version") ?? "4.0",
                Uuid = GetAttribute(timbre, "UUID") ?? fiscalStamp.Uuid ?? "N/D",
                SeriesFolio = CombineDocumentNumber(GetAttribute(comprobante, "Serie"), GetAttribute(comprobante, "Folio")),
                IssuedAt = GetAttribute(comprobante, "Fecha") ?? FormatUtc(fiscalDocument.IssuedAtUtc),
                StampedAt = GetAttribute(timbre, "FechaTimbrado") ?? FormatUtc(fiscalStamp.StampedAtUtc),
                PaymentMethod = string.IsNullOrWhiteSpace(paymentMethodCode)
                    ? string.Empty
                    : satCatalogDescriptionProvider.FormatPaymentMethod(paymentMethodCode),
                PaymentForm = string.IsNullOrWhiteSpace(paymentFormCode)
                    ? string.Empty
                    : satCatalogDescriptionProvider.FormatPaymentForm(paymentFormCode),
                ExportCode = satCatalogDescriptionProvider.FormatExportCode(GetAttribute(comprobante, "Exportacion") ?? "01"),
                Currency = GetAttribute(comprobante, "Moneda") ?? fiscalDocument.CurrencyCode,
                PlaceOfIssue = GetAttribute(comprobante, "LugarExpedicion") ?? fiscalDocument.IssuerPostalCode,
                IssuerName = GetAttribute(emisor, "Nombre") ?? fiscalDocument.IssuerLegalName,
                IssuerRfc = GetAttribute(emisor, "Rfc") ?? fiscalDocument.IssuerRfc,
                IssuerRegime = satCatalogDescriptionProvider.FormatFiscalRegime(GetAttribute(emisor, "RegimenFiscal") ?? fiscalDocument.IssuerFiscalRegimeCode),
                IssuerCertificate = GetAttribute(comprobante, "NoCertificado"),
                ReceiverName = GetAttribute(receptor, "Nombre") ?? fiscalDocument.ReceiverLegalName,
                ReceiverRfc = GetAttribute(receptor, "Rfc") ?? fiscalDocument.ReceiverRfc,
                ReceiverUse = satCatalogDescriptionProvider.FormatCfdiUse(GetAttribute(receptor, "UsoCFDI") ?? fiscalDocument.ReceiverCfdiUseCode),
                ReceiverRegime = satCatalogDescriptionProvider.FormatFiscalRegime(GetAttribute(receptor, "RegimenFiscalReceptor") ?? fiscalDocument.ReceiverFiscalRegimeCode),
                ReceiverPostalCode = GetAttribute(receptor, "DomicilioFiscalReceptor") ?? fiscalDocument.ReceiverPostalCode,
                TotalInWords = string.Equals(GetAttribute(comprobante, "Moneda") ?? fiscalDocument.CurrencyCode, "MXN", StringComparison.OrdinalIgnoreCase)
                    ? SpanishCurrencyTextFormatter.FormatMx(total)
                    : $"{total.ToString("0.00", CultureInfo.InvariantCulture)} {(GetAttribute(comprobante, "Moneda") ?? fiscalDocument.CurrencyCode)}",
                Subtotal = FormatMoney(GetAttribute(comprobante, "SubTotal"), fiscalDocument.Subtotal),
                Discount = FormatMoney(GetAttribute(comprobante, "Descuento"), fiscalDocument.DiscountTotal),
                Taxes = ResolveTransferredTaxes(document, fiscalDocument).ToString("0.00", CultureInfo.InvariantCulture),
                Total = FormatMoney(GetAttribute(comprobante, "Total"), total),
                Qr = ResolveQrText(fiscalStamp, comprobante, timbre, emisor, receptor),
                QrPayload = ResolveQrPayload(fiscalStamp, comprobante, timbre, emisor, receptor),
                OriginalString = fiscalStamp.OriginalString,
                SatCertificate = GetAttribute(timbre, "NoCertificadoSAT"),
                CfdiSeal = GetAttribute(comprobante, "Sello"),
                SatSeal = GetAttribute(timbre, "SelloSAT"),
                ProviderName = fiscalStamp.ProviderName,
                AdditionalFields = fiscalDocument.SpecialFieldValues
                    .OrderBy(x => x.DisplayOrder)
                    .Select(x => new PdfAdditionalFieldRow(x.FieldCode, x.FieldLabelSnapshot, x.Value))
                    .ToArray(),
                Concepts = conceptos.Count == 0
                    ? [new PdfConceptRow("N/D", "N/D", "0", "N/D", "N/D", "No se encontraron conceptos en el XML timbrado.", "N/D", "0", "0")]
                    : conceptos.Select(MapConcept).ToArray()
                ,
                TaxesBreakdown = ResolveTaxBreakdown(document)
            };
        }

        private static string? ResolveQrText(FiscalStamp fiscalStamp, XElement? comprobante, XElement? timbre, XElement? emisor, XElement? receptor)
            => fiscalStamp.QrCodeTextOrUrl ?? ResolveQrPayload(fiscalStamp, comprobante, timbre, emisor, receptor);

        private static string? ResolveQrPayload(FiscalStamp fiscalStamp, XElement? comprobante, XElement? timbre, XElement? emisor, XElement? receptor)
        {
            if (!string.IsNullOrWhiteSpace(fiscalStamp.QrCodeTextOrUrl))
            {
                return fiscalStamp.QrCodeTextOrUrl.Trim();
            }

            var uuid = GetAttribute(timbre, "UUID");
            var issuerRfc = GetAttribute(emisor, "Rfc");
            var receiverRfc = GetAttribute(receptor, "Rfc");
            var total = GetAttribute(comprobante, "Total");
            var sello = GetAttribute(comprobante, "Sello");
            if (string.IsNullOrWhiteSpace(uuid)
                || string.IsNullOrWhiteSpace(issuerRfc)
                || string.IsNullOrWhiteSpace(receiverRfc)
                || string.IsNullOrWhiteSpace(total)
                || string.IsNullOrWhiteSpace(sello)
                || sello.Length < 8)
            {
                return null;
            }

            return $"https://verificacfdi.facturaelectronica.sat.gob.mx/default.aspx?id={Uri.EscapeDataString(uuid)}&re={Uri.EscapeDataString(issuerRfc)}&rr={Uri.EscapeDataString(receiverRfc)}&tt={Uri.EscapeDataString(total)}&fe={Uri.EscapeDataString(sello[^8..])}";
        }

        private static PdfConceptRow MapConcept(XElement concept)
        {
            return new PdfConceptRow(
                GetAttribute(concept, "ClaveProdServ") ?? "N/D",
                GetAttribute(concept, "NoIdentificacion") ?? "N/D",
                GetAttribute(concept, "Cantidad") ?? "0",
                GetAttribute(concept, "ClaveUnidad") ?? "N/D",
                GetAttribute(concept, "Unidad") ?? "N/D",
                GetAttribute(concept, "Descripcion") ?? "Concepto",
                GetAttribute(concept, "ObjetoImp") ?? "N/D",
                FormatMoney(GetAttribute(concept, "ValorUnitario"), 0m),
                FormatMoney(GetAttribute(concept, "Importe"), 0m));
        }

        private static IReadOnlyList<PdfTaxRow> ResolveTaxBreakdown(XDocument document)
        {
            return document
                .Descendants()
                .Where(static node => node.Name.LocalName == "Traslado")
                .Select(static traslado => new PdfTaxRow(
                    $"{GetAttribute(traslado, "Impuesto") ?? "Impuesto"} {GetAttribute(traslado, "TipoFactor") ?? string.Empty}".Trim(),
                    GetAttribute(traslado, "TasaOCuota") ?? "N/D",
                    FormatMoney(GetAttribute(traslado, "Importe"), 0m)))
                .ToArray();
        }

        private static decimal ParseDecimal(string? value, decimal fallback)
            => decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;

        private static string FormatMoney(string? value, decimal fallback)
            => ParseDecimal(value, fallback).ToString("0.00", CultureInfo.InvariantCulture);

        private static string ResolveDocumentType(string? type)
        {
            return type?.ToUpperInvariant() switch
            {
                "I" => "FACTURA",
                "E" => "NOTA DE CREDITO",
                "P" => "COMPLEMENTO DE PAGO",
                "N" => "NOMINA",
                "T" => "TRASLADO",
                _ => "CFDI"
            };
        }
    }

    private sealed record PdfConceptRow(
        string ProductCode,
        string Identification,
        string Quantity,
        string UnitCode,
        string UnitText,
        string Description,
        string TaxObject,
        string UnitPrice,
        string Amount);

    private sealed record PdfTaxRow(string Label, string Rate, string Amount);

    private sealed record PdfAdditionalFieldRow(string Code, string Label, string Value);
    private sealed record RepField(string Label, string Value, bool PreferFullWidth = false);
    private sealed record RepTaxRow(string RowType, string Base, string Tax, string FactorType, string Rate, string Amount, string RawValue);
    private sealed record RepRelatedDocumentSection(int PaymentNumber, int DocumentNumber, IReadOnlyList<RepField> Fields, IReadOnlyList<RepTaxRow> Taxes);
    private sealed record RepPaymentSection(int PaymentNumber, IReadOnlyList<RepField> Fields, IReadOnlyList<RepTaxRow> Taxes, IReadOnlyList<RepRelatedDocumentSection> Documents);
    private sealed record RepAdditionalLayout(IReadOnlyList<RepField> ComplementFields, IReadOnlyList<RepPaymentSection> Payments);
    private sealed class RepPaymentSectionBuilder
    {
        public List<RepField> Fields { get; } = [];
        public List<RepTaxRow> Taxes { get; } = [];
        public SortedDictionary<int, RepRelatedDocumentSectionBuilder> Documents { get; } = [];
    }

    private sealed class RepRelatedDocumentSectionBuilder
    {
        public List<RepField> Fields { get; } = [];
        public List<RepTaxRow> Taxes { get; } = [];
    }

    private static bool TryBuildRepAdditionalLayout(IReadOnlyList<PdfAdditionalFieldRow> fields, out RepAdditionalLayout layout)
    {
        if (!fields.Any(static field => field.Code.StartsWith("REP_", StringComparison.OrdinalIgnoreCase)))
        {
            layout = new RepAdditionalLayout([], []);
            return false;
        }

        var complementFields = new List<RepField>();
        var paymentBuilders = new SortedDictionary<int, RepPaymentSectionBuilder>();

        foreach (var field in fields)
        {
            if (string.IsNullOrWhiteSpace(field.Code))
            {
                continue;
            }

            if (field.Code.StartsWith("REP_COMPLEMENT_FIELD_", StringComparison.OrdinalIgnoreCase))
            {
                complementFields.Add(CreateRepField(field));
                continue;
            }

            var paymentFieldMatch = RepPaymentFieldCodePattern.Match(field.Code);
            if (paymentFieldMatch.Success)
            {
                var paymentNumber = ParsePositiveInt(paymentFieldMatch.Groups[1].Value);
                if (paymentNumber > 0)
                {
                    GetOrCreatePaymentBuilder(paymentBuilders, paymentNumber).Fields.Add(CreateRepField(field));
                }

                continue;
            }

            var paymentTaxMatch = RepPaymentTaxCodePattern.Match(field.Code);
            if (paymentTaxMatch.Success)
            {
                var paymentNumber = ParsePositiveInt(paymentTaxMatch.Groups[1].Value);
                if (paymentNumber > 0)
                {
                    GetOrCreatePaymentBuilder(paymentBuilders, paymentNumber).Taxes.Add(CreateRepTaxRow(field));
                }

                continue;
            }

            var documentFieldMatch = RepDocumentFieldCodePattern.Match(field.Code);
            if (documentFieldMatch.Success)
            {
                var paymentNumber = ParsePositiveInt(documentFieldMatch.Groups[1].Value);
                var documentNumber = ParsePositiveInt(documentFieldMatch.Groups[2].Value);
                if (paymentNumber > 0 && documentNumber > 0)
                {
                    GetOrCreateDocumentBuilder(paymentBuilders, paymentNumber, documentNumber).Fields.Add(CreateRepField(field));
                }

                continue;
            }

            var documentTaxMatch = RepDocumentTaxCodePattern.Match(field.Code);
            if (documentTaxMatch.Success)
            {
                var paymentNumber = ParsePositiveInt(documentTaxMatch.Groups[1].Value);
                var documentNumber = ParsePositiveInt(documentTaxMatch.Groups[2].Value);
                if (paymentNumber > 0 && documentNumber > 0)
                {
                    GetOrCreateDocumentBuilder(paymentBuilders, paymentNumber, documentNumber).Taxes.Add(CreateRepTaxRow(field));
                }
            }
        }

        layout = new RepAdditionalLayout(
            complementFields,
            paymentBuilders
                .Select(static paymentEntry => new RepPaymentSection(
                    paymentEntry.Key,
                    paymentEntry.Value.Fields,
                    paymentEntry.Value.Taxes,
                    paymentEntry.Value.Documents
                        .Select(documentEntry => new RepRelatedDocumentSection(
                            paymentEntry.Key,
                            documentEntry.Key,
                            documentEntry.Value.Fields,
                            documentEntry.Value.Taxes))
                        .ToArray()))
                .ToArray());

        return layout.ComplementFields.Count > 0 || layout.Payments.Count > 0;
    }

    private static RepField CreateRepField(PdfAdditionalFieldRow field)
        => new(field.Label, field.Value, ShouldRenderRepFieldFullWidth(field));

    private static bool ShouldRenderRepFieldFullWidth(PdfAdditionalFieldRow field)
    {
        if (field.Code.Contains("IDDOCUMENTO", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (field.Value.Length >= 56)
        {
            return true;
        }

        return field.Value.Length >= 36 && !field.Value.Contains(' ', StringComparison.Ordinal);
    }

    private static RepTaxRow CreateRepTaxRow(PdfAdditionalFieldRow field)
    {
        var attributes = ParseRepAttributes(field.Value);
        return new RepTaxRow(
            field.Label,
            GetRepAttribute(attributes, "BaseP", "BaseDR", "Base"),
            GetRepAttribute(attributes, "ImpuestoP", "ImpuestoDR", "Impuesto"),
            GetRepAttribute(attributes, "TipoFactorP", "TipoFactorDR", "TipoFactor"),
            GetRepAttribute(attributes, "TasaOCuotaP", "TasaOCuotaDR", "TasaOCuota"),
            GetRepAttribute(attributes, "ImporteP", "ImporteDR", "Importe"),
            field.Value);
    }

    private static IReadOnlyDictionary<string, string> ParseRepAttributes(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var segment in rawValue.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separatorIndex = segment.IndexOf('=');
            if (separatorIndex <= 0 || separatorIndex >= segment.Length - 1)
            {
                continue;
            }

            var key = segment[..separatorIndex].Trim();
            var value = segment[(separatorIndex + 1)..].Trim();
            if (key.Length == 0 || value.Length == 0)
            {
                continue;
            }

            values[key] = value;
        }

        return values;
    }

    private static string GetRepAttribute(IReadOnlyDictionary<string, string> attributes, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (attributes.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return "N/D";
    }

    private static RepPaymentSectionBuilder GetOrCreatePaymentBuilder(IDictionary<int, RepPaymentSectionBuilder> paymentBuilders, int paymentNumber)
    {
        if (!paymentBuilders.TryGetValue(paymentNumber, out var builder))
        {
            builder = new RepPaymentSectionBuilder();
            paymentBuilders[paymentNumber] = builder;
        }

        return builder;
    }

    private static RepRelatedDocumentSectionBuilder GetOrCreateDocumentBuilder(IDictionary<int, RepPaymentSectionBuilder> paymentBuilders, int paymentNumber, int documentNumber)
    {
        var paymentBuilder = GetOrCreatePaymentBuilder(paymentBuilders, paymentNumber);
        if (!paymentBuilder.Documents.TryGetValue(documentNumber, out var documentBuilder))
        {
            documentBuilder = new RepRelatedDocumentSectionBuilder();
            paymentBuilder.Documents[documentNumber] = documentBuilder;
        }

        return documentBuilder;
    }

    private static int ParsePositiveInt(string value)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0 ? parsed : 0;

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

    private sealed class FiscalPdfDocument
    {
        private const float PageWidth = 612f;
        private const float PageHeight = 792f;
        private const float Margin = 10f;
        private const float SectionGap = 4f;
        private const float FooterReserve = 30f;

        private readonly List<PdfPageBuilder> _pages = [];
        private PdfPageBuilder _page;
        private PdfViewModel? _model;
        private float _cursorY;

        private FiscalPdfDocument()
        {
            _page = new PdfPageBuilder(PageWidth, PageHeight);
            _pages.Add(_page);
            _cursorY = PageHeight - Margin;
        }

        public static byte[] Create(PdfViewModel model, PdfImageAsset? logo, PdfImageAsset? qr)
        {
            return new FiscalPdfDocument().Build(model, logo, qr);
        }

        private byte[] Build(PdfViewModel model, PdfImageAsset? logo, PdfImageAsset? qr)
        {
            _model = model;
            DrawHeader(model, logo);
            DrawReceiverSection(model);
            DrawAdditionalFields(model.AdditionalFields);
            DrawConceptTable(model.Concepts);
            DrawTotals(model);
            DrawTimbre(model, qr);
            DrawFooter(model);
            return PdfPageBuilder.BuildDocument(_pages);
        }

        private float ContentBottom => Margin + FooterReserve;

        private bool HasSpace(float requiredHeight) => (_cursorY - requiredHeight) >= ContentBottom;

        private void EnsureSpace(float requiredHeight)
        {
            if (!HasSpace(requiredHeight))
            {
                StartNewPage();
            }
        }

        private void StartNewPage()
        {
            if (_model is not null)
            {
                DrawFooter(_model);
            }

            _page = new PdfPageBuilder(PageWidth, PageHeight);
            _pages.Add(_page);
            _cursorY = PageHeight - Margin;
        }

        private void DrawHeader(PdfViewModel model, PdfImageAsset? logo)
        {
            var topY = _cursorY;
            var fiscalRows = new List<(string Label, string Value)>
            {
                ("Version", model.CfdiVersion),
                ("Folio", model.SeriesFolio),
                ("Folio fiscal", model.Uuid)
            };

            if (!string.IsNullOrWhiteSpace(model.PaymentForm))
            {
                fiscalRows.Add(("Forma de pago", model.PaymentForm));
            }

            if (!string.IsNullOrWhiteSpace(model.PaymentMethod))
            {
                fiscalRows.Add(("Metodo de pago", model.PaymentMethod));
            }

            fiscalRows.Add(("Exportacion", model.ExportCode));
            fiscalRows.Add(("Regimen fiscal", model.IssuerRegime));

            var rightBoxWidth = 220f;
            var rightBoxInnerWidth = rightBoxWidth - 20f;
            float lineHeight = 9f;
            float labelFontSize = 8.5f;
            float valueFontSize = 7.5f;

            // Calcular altura: cada fila es una sola línea (etiqueta + valor inline, sin columna fija)
            var rightBoxContentHeight = fiscalRows.Count * (lineHeight + 2f);

            var headerHeight = Math.Max(160f, 24f + Math.Max(132f, rightBoxContentHeight + 18f));
            _page.FillRectangle(Margin, topY - headerHeight, PageWidth - (Margin * 2), headerHeight, PdfColor.White);

            // Logo (sin borde)
            var logoBoxX = Margin + 12f;
            var logoBoxY = topY - 128f;
            var logoBoxW = 92f;
            var logoBoxH = 86f;
            _page.FillRectangle(logoBoxX, logoBoxY, logoBoxW, logoBoxH, PdfColor.White);

            if (logo is not null)
            {
                var fitted = Fit(logo.Width, logo.Height, logoBoxW - 8f, logoBoxH - 8f);
                var imageX = logoBoxX + ((logoBoxW - fitted.Width) / 2f);
                var imageY = logoBoxY + ((logoBoxH - fitted.Height) / 2f);
                _page.DrawImage(logo, imageX, imageY, fitted.Width, fitted.Height);
            }
            else
            {
                _page.DrawTextCentered("CFDI 4.0", logoBoxX, logoBoxX + logoBoxW, logoBoxY + 42f, 18f, PdfFont.Bold, new PdfColor(82, 88, 102));
            }

            // Columna central (datos del emisor)
            var infoX = PageWidth - Margin - rightBoxWidth;
            var centerLeft = logoBoxX + logoBoxW + 12f;
            var centerRight = infoX - 12f;

            _page.DrawText(model.IssuerName, centerLeft, topY - 48f, 14f, PdfFont.Bold, new PdfColor(22, 30, 42));

            float y = topY - 70f;
            _page.DrawText("RFC:", centerLeft, y, 9f, PdfFont.Bold, new PdfColor(70, 78, 92));
            _page.DrawText(model.IssuerRfc, centerLeft + 30f, y, 9f, PdfFont.Regular, new PdfColor(70, 78, 92));

            y -= 16f;
            _page.DrawText("RÉGIMEN FISCAL:", centerLeft, y, 9f, PdfFont.Bold, new PdfColor(70, 78, 92));
            _page.DrawText(model.IssuerRegime, centerLeft + 100f, y, 9f, PdfFont.Regular, new PdfColor(76, 84, 96));

            y -= 16f;
            _page.DrawText("LUGAR DE EXPEDICIÓN:", centerLeft, y, 9f, PdfFont.Bold, new PdfColor(70, 78, 92));
            _page.DrawText(model.PlaceOfIssue, centerLeft + 120f, y, 9f, PdfFont.Regular, new PdfColor(76, 84, 96));

            y -= 16f;
            _page.DrawText("FECHA Y HORA DE EXPEDICIÓN:", centerLeft, y, 9f, PdfFont.Bold, new PdfColor(70, 78, 92));
            _page.DrawText(model.IssuedAt, centerLeft + 150f, y, 9f, PdfFont.Regular, new PdfColor(76, 84, 96));

            y -= 16f;
            _page.DrawText("SUCURSAL:", centerLeft, y, 9f, PdfFont.Bold, new PdfColor(70, 78, 92));
            _page.DrawText("FISCALDOM", centerLeft + 60f, y, 9f, PdfFont.Regular, new PdfColor(76, 84, 96));

            // Columna derecha (Datos fiscales) con ancho fijo de etiqueta
            var infoTopY = topY - 34f;
            var infoHeight = Math.Max(112f, rightBoxContentHeight + 18f);
            _page.FillRectangle(infoX, infoTopY - infoHeight, rightBoxWidth, infoHeight, new PdfColor(255, 255, 255));
            _page.StrokeRectangle(infoX, infoTopY - infoHeight, rightBoxWidth, infoHeight, new PdfColor(205, 199, 189), 0.8f);
            _page.FillRectangle(infoX, infoTopY - 18f, rightBoxWidth, 18f, new PdfColor(238, 235, 228));
            _page.DrawTextCentered($"Datos fiscales - {model.DocumentType}", infoX, infoX + rightBoxWidth, infoTopY - 12f, 8.5f, PdfFont.Bold, new PdfColor(88, 80, 66));

            var rowY = infoTopY - 28f;
            foreach (var row in fiscalRows)
            {
                rowY = DrawInlineKeyValueCompact(row.Label, row.Value, infoX + 10f, rowY, rightBoxInnerWidth, labelFontSize, valueFontSize, lineHeight);
            }

            _cursorY = topY - headerHeight - SectionGap;
        }

        private float DrawInlineKeyValueFixed(string label, string value, float x, float y, float labelWidth, float valueWidth, float labelFontSize, float valueFontSize, float lineHeight)
        {
            _page.DrawText($"{label}:", x, y, labelFontSize, PdfFont.Bold, new PdfColor(120, 108, 84));
            var availableWidth = valueWidth;
            var lines = WrapText(value, EstimateWrapLength(availableWidth, valueFontSize, PdfFont.Regular)).ToArray();

            if (lines.Length == 0)
            {
                return y - lineHeight;
            }

            _page.DrawText(lines[0], x + labelWidth, y, valueFontSize, PdfFont.Regular, new PdfColor(50, 58, 70));

            var currentY = y - lineHeight;
            for (int i = 1; i < lines.Length; i++)
            {
                _page.DrawText(lines[i], x + labelWidth, currentY, valueFontSize, PdfFont.Regular, new PdfColor(50, 58, 70));
                currentY -= lineHeight;
            }

            return currentY - 2f;
        }

        private static string FormatTimbreLabel(string label) => label.Trim().ToUpperInvariant();

        // Renders label + value inline: "Label: valor" with proper indentation for wrapped lines
        private float DrawInlineKeyValueCompact(string label, string value, float x, float y, float maxWidth, float labelFontSize, float valueFontSize, float lineHeight)
        {
            var labelText = $"{label}: ";
            // Use full estimated width plus a small gap (no reduction)
            var labelW = EstimateTextWidth(labelText, labelFontSize, PdfFont.Bold) * 0.86f + 2f;
            _page.DrawText(labelText, x, y, labelFontSize, PdfFont.Bold, new PdfColor(120, 108, 84));

            var availableWidth = maxWidth - labelW;
            if (availableWidth < 20f) availableWidth = 20f;
            var lines = WrapText(value, EstimateWrapLength(availableWidth, valueFontSize, PdfFont.Regular)).ToArray();

            if (lines.Length == 0)
            {
                return y - lineHeight - 2f;
            }

            // Draw first line indented
            _page.DrawText(lines[0], x + labelW, y, valueFontSize, PdfFont.Regular, new PdfColor(50, 58, 70));

            var currentY = y - lineHeight;
            for (int i = 1; i < lines.Length; i++)
            {
                // Subsequent lines also indented by labelW
                _page.DrawText(lines[i], x + labelW, currentY, valueFontSize, PdfFont.Regular, new PdfColor(50, 58, 70));
                currentY -= lineHeight;
            }

            return currentY - 2f;
        }

        private void DrawReceiverSection(PdfViewModel model)
        {
            const float innerPad = 10f;
            const float columnGap = 14f;
            const float headerHeight = 20f;
            const float topPad = 10f;
            const float bottomPad = 12f;
            const float nameFontSize = 11f;
            const float nameLineHeight = 13f;
            const float fieldLabelFontSize = 9f;
            const float fieldValueFontSize = 9.5f;
            const float fieldLabelLineHeight = 10f;
            const float fieldValueLineHeight = 10.5f;

            var sectionWidth = PageWidth - (Margin * 2);
            var contentWidth = sectionWidth - (innerPad * 2);
            var leftWidth = (contentWidth - columnGap) * 0.3f;
            var rightWidth = contentWidth - columnGap - leftWidth;
            var leftX = Margin + innerPad;
            var rightX = leftX + leftWidth + columnGap;
            var receiverNameLines = WrapTextConservatively(model.ReceiverName, contentWidth, nameFontSize, isBold: true);
            var receiverNameHeight = Math.Max(nameLineHeight, receiverNameLines.Length * nameLineHeight);

            var leftHeight =
                MeasureStackedFieldHeight(model.ReceiverRfc, leftWidth, fieldValueFontSize, fieldLabelLineHeight, fieldValueLineHeight) +
                MeasureStackedFieldHeight(model.ReceiverPostalCode, leftWidth, fieldValueFontSize, fieldLabelLineHeight, fieldValueLineHeight);
            var rightHeight =
                MeasureStackedFieldHeight(model.ReceiverRegime, rightWidth, fieldValueFontSize, fieldLabelLineHeight, fieldValueLineHeight) +
                MeasureStackedFieldHeight(model.ReceiverUse, rightWidth, fieldValueFontSize, fieldLabelLineHeight, fieldValueLineHeight);
            var sectionHeight = headerHeight + topPad + receiverNameHeight + 8f + Math.Max(leftHeight, rightHeight) + bottomPad;

            EnsureSpace(sectionHeight + SectionGap);

            var topY = _cursorY;
            var columnsStartY = topY - headerHeight - topPad - receiverNameHeight - 8f;

            _page.FillRectangle(Margin, topY - sectionHeight, sectionWidth, sectionHeight, PdfColor.White);
            _page.StrokeRectangle(Margin, topY - sectionHeight, sectionWidth, sectionHeight, new PdfColor(218, 213, 204), 0.8f);
            _page.FillRectangle(Margin, topY - headerHeight, sectionWidth, headerHeight, new PdfColor(238, 235, 228));
            _page.DrawText("Datos del cliente / receptor", Margin + 10f, topY - 14f, 10f, PdfFont.Bold, new PdfColor(22, 30, 42));

            var nameY = topY - headerHeight - topPad;
            DrawWrappedTextLines(receiverNameLines, leftX, nameY, nameFontSize, nameLineHeight, PdfFont.Bold, new PdfColor(22, 30, 42));

            var leftY = columnsStartY;
            leftY = DrawStackedField("RFC", model.ReceiverRfc, leftX, leftY, leftWidth, fieldLabelFontSize, fieldValueFontSize, fieldLabelLineHeight, fieldValueLineHeight);
            leftY = DrawStackedField("Domicilio Fiscal", model.ReceiverPostalCode, leftX, leftY, leftWidth, fieldLabelFontSize, fieldValueFontSize, fieldLabelLineHeight, fieldValueLineHeight);

            var rightY = columnsStartY;
            rightY = DrawStackedField("Régimen fiscal", model.ReceiverRegime, rightX, rightY, rightWidth, fieldLabelFontSize, fieldValueFontSize, fieldLabelLineHeight, fieldValueLineHeight);
            rightY = DrawStackedField("Uso CFDI", model.ReceiverUse, rightX, rightY, rightWidth, fieldLabelFontSize, fieldValueFontSize, fieldLabelLineHeight, fieldValueLineHeight);

            _cursorY = topY - sectionHeight - SectionGap;
        }

        private void DrawConceptTable(IReadOnlyList<PdfConceptRow> concepts)
        {
            var tableX = Margin;
            var tableWidth = PageWidth - (Margin * 2);
            var headerHeight = 24f;
            var descriptionLinesByRow = concepts
                .Select(row => WrapText(row.Description, 28).ToArray())
                .ToArray();
            var rowHeights = descriptionLinesByRow
                .Select(lines => Math.Max(30f, 16f + (Math.Max(1, lines.Length) * 10f)))
                .ToArray();
            var columns = new[] { 62f, 52f, 38f, 46f, 46f, 144f, 38f, 54f, 52f };
            var labels = new[] { "Clave prod./serv.", "No. id", "Cant.", "Clave u.", "Unidad", "Descripcion", "Obj. imp.", "V. unitario", "Importe" };

            void DrawTableHeader()
            {
                EnsureSpace(headerHeight + 30f);
                _page.FillRectangle(tableX, _cursorY - headerHeight, tableWidth, headerHeight, new PdfColor(32, 42, 56));

                var headerCursorX = tableX;
                for (var columnIndex = 0; columnIndex < columns.Length; columnIndex++)
                {
                    _page.DrawText(labels[columnIndex], headerCursorX + 4f, _cursorY - 16f, 7.5f, PdfFont.Bold, PdfColor.White);
                    headerCursorX += columns[columnIndex];
                    if (columnIndex < columns.Length - 1)
                    {
                        _page.DrawLine(headerCursorX, _cursorY, headerCursorX, _cursorY - headerHeight, new PdfColor(226, 226, 226), 0.5f);
                    }
                }

                _cursorY -= headerHeight;
            }

            DrawTableHeader();

            for (var index = 0; index < concepts.Count; index++)
            {
                var rowHeight = rowHeights[index];
                if (!HasSpace(rowHeight + SectionGap))
                {
                    StartNewPage();
                    DrawTableHeader();
                }

                var rowTop = _cursorY;
                if (index % 2 == 0)
                {
                    _page.FillRectangle(tableX, rowTop - rowHeight, tableWidth, rowHeight, new PdfColor(250, 250, 248));
                }

                _page.StrokeRectangle(tableX, rowTop - rowHeight, tableWidth, rowHeight, new PdfColor(218, 213, 204), 0.4f);

                var row = concepts[index];
                var x = tableX;
                _page.DrawText(row.ProductCode, x + 4f, rowTop - 16f, 7.5f, PdfFont.Regular, new PdfColor(50, 58, 70));
                x += columns[0];
                _page.DrawText(row.Identification, x + 4f, rowTop - 16f, 7.5f, PdfFont.Regular, new PdfColor(50, 58, 70));
                x += columns[1];
                _page.DrawText(row.Quantity, x + 4f, rowTop - 16f, 7.5f, PdfFont.Regular, new PdfColor(50, 58, 70));
                x += columns[2];
                _page.DrawText(row.UnitCode, x + 4f, rowTop - 16f, 7.5f, PdfFont.Regular, new PdfColor(50, 58, 70));
                x += columns[3];
                _page.DrawText(row.UnitText, x + 4f, rowTop - 16f, 7.5f, PdfFont.Regular, new PdfColor(50, 58, 70));
                x += columns[4];

                var descriptionLines = descriptionLinesByRow[index];
                _page.DrawText(descriptionLines[0], x + 4f, rowTop - 14f, 7.8f, PdfFont.Regular, new PdfColor(50, 58, 70));
                for (var lineIndex = 1; lineIndex < descriptionLines.Length; lineIndex++)
                {
                    _page.DrawText(descriptionLines[lineIndex], x + 4f, rowTop - 14f - (lineIndex * 10f), 7.2f, PdfFont.Regular, new PdfColor(92, 98, 110));
                }

                x += columns[5];
                _page.DrawText(row.TaxObject, x + 4f, rowTop - 16f, 7.5f, PdfFont.Regular, new PdfColor(50, 58, 70));
                x += columns[6];
                _page.DrawText(row.UnitPrice, x + 4f, rowTop - 16f, 7.5f, PdfFont.Regular, new PdfColor(50, 58, 70));
                x += columns[7];
                _page.DrawText(row.Amount, x + 4f, rowTop - 16f, 7.5f, PdfFont.Bold, new PdfColor(50, 58, 70));

                _page.DrawLine(tableX, rowTop - rowHeight, tableX + tableWidth, rowTop - rowHeight, new PdfColor(230, 230, 230), 0.5f);
                _cursorY -= rowHeight;
            }

            _cursorY -= SectionGap;
        }

        private void DrawAdditionalFields(IReadOnlyList<PdfAdditionalFieldRow> fields)
        {
            if (fields.Count == 0)
            {
                return;
            }

            if (TryBuildRepAdditionalLayout(fields, out var repLayout))
            {
                DrawRepAdditionalFields(repLayout);
                return;
            }

            DrawGenericAdditionalFields(fields);
        }

        private void DrawGenericAdditionalFields(IReadOnlyList<PdfAdditionalFieldRow> fields)
        {
            var genericFields = fields
                .Select(CreateRepField)
                .ToArray();
            var blockHeight = MeasureRepFieldBlockHeight(genericFields);
            EnsureSpace(blockHeight + SectionGap);
            DrawRepFieldBlock("Datos adicionales", genericFields);
        }

        private void DrawRepAdditionalFields(RepAdditionalLayout layout)
        {
            var sectionHeaderDrawn = false;

            if (layout.ComplementFields.Count > 0)
            {
                DrawRepFieldBlockWithSectionHeader("Datos del complemento", layout.ComplementFields, ref sectionHeaderDrawn);
            }

            foreach (var payment in layout.Payments)
            {
                DrawRepFieldBlockWithSectionHeader($"Pago {payment.PaymentNumber}", payment.Fields, ref sectionHeaderDrawn);

                if (payment.Taxes.Count > 0)
                {
                    DrawRepTaxBlockWithSectionHeader($"Pago {payment.PaymentNumber} - Impuestos", payment.Taxes, ref sectionHeaderDrawn);
                }

                foreach (var document in payment.Documents)
                {
                    DrawRepFieldBlockWithSectionHeader(
                        $"Pago {document.PaymentNumber} - Documento relacionado {document.DocumentNumber}",
                        document.Fields,
                        ref sectionHeaderDrawn);

                    if (document.Taxes.Count > 0)
                    {
                        DrawRepTaxBlockWithSectionHeader(
                            $"Pago {document.PaymentNumber} - Documento relacionado {document.DocumentNumber} - Impuestos",
                            document.Taxes,
                            ref sectionHeaderDrawn);
                    }
                }
            }
        }

        private void DrawRepFieldBlockWithSectionHeader(string title, IReadOnlyList<RepField> fields, ref bool sectionHeaderDrawn)
        {
            if (fields.Count == 0)
            {
                return;
            }

            var blockHeight = MeasureRepFieldBlockHeight(fields);
            EnsureRepAdditionalSectionHeader(ref sectionHeaderDrawn, blockHeight + SectionGap);
            DrawRepFieldBlock(title, fields);
        }

        private void DrawRepTaxBlockWithSectionHeader(string title, IReadOnlyList<RepTaxRow> rows, ref bool sectionHeaderDrawn)
        {
            if (rows.Count == 0)
            {
                return;
            }

            var blockHeight = MeasureRepTaxBlockHeight(rows);
            EnsureRepAdditionalSectionHeader(ref sectionHeaderDrawn, blockHeight + SectionGap);
            DrawRepTaxBlock(title, rows);
        }

        private void EnsureRepAdditionalSectionHeader(ref bool sectionHeaderDrawn, float requiredHeight)
        {
            var headerReserve = sectionHeaderDrawn ? 0f : 30f;
            if (!HasSpace(requiredHeight + headerReserve))
            {
                StartNewPage();
                sectionHeaderDrawn = false;
            }

            if (!sectionHeaderDrawn)
            {
                DrawAdditionalSectionHeader();
                sectionHeaderDrawn = true;
            }
        }

        private void DrawAdditionalSectionHeader()
        {
            const float headerHeight = 20f;
            var sectionWidth = PageWidth - (Margin * 2);
            var topY = _cursorY;

            _page.FillRectangle(Margin, topY - headerHeight, sectionWidth, headerHeight, new PdfColor(238, 235, 228));
            _page.StrokeRectangle(Margin, topY - headerHeight, sectionWidth, headerHeight, new PdfColor(218, 213, 204), 0.8f);
            _page.DrawText("Datos adicionales", Margin + 10f, topY - 14f, 10f, PdfFont.Bold, new PdfColor(22, 30, 42));
            _cursorY = topY - headerHeight - 6f;
        }

        private void DrawRepFieldBlock(string title, IReadOnlyList<RepField> fields)
        {
            const float headerHeight = 18f;
            const float innerPad = 10f;
            const float topPad = 10f;
            const float columnGap = 14f;
            const float rowGap = 4f;
            const float labelFontSize = 8.5f;
            const float valueFontSize = 8.7f;
            const float labelLineHeight = 10f;
            const float valueLineHeight = 10f;

            var sectionWidth = PageWidth - (Margin * 2);
            var contentWidth = sectionWidth - (innerPad * 2);
            var columnWidth = (contentWidth - columnGap) / 2f;
            var rows = BuildRepFieldRows(fields);
            var blockHeight = MeasureRepFieldBlockHeight(fields);
            var topY = _cursorY;

            _page.FillRectangle(Margin, topY - blockHeight, sectionWidth, blockHeight, PdfColor.White);
            _page.StrokeRectangle(Margin, topY - blockHeight, sectionWidth, blockHeight, new PdfColor(218, 213, 204), 0.8f);
            _page.FillRectangle(Margin, topY - headerHeight, sectionWidth, headerHeight, new PdfColor(248, 247, 243));
            _page.DrawText(title, Margin + 10f, topY - 13f, 9f, PdfFont.Bold, new PdfColor(88, 80, 66));

            var leftX = Margin + innerPad;
            var rightX = leftX + columnWidth + columnGap;
            var currentY = topY - headerHeight - topPad;

            foreach (var row in rows)
            {
                if (row.Second is null)
                {
                    currentY = DrawStackedField(row.First.Label, row.First.Value, leftX, currentY, contentWidth, labelFontSize, valueFontSize, labelLineHeight, valueLineHeight) - rowGap;
                    continue;
                }

                var leftEndY = DrawStackedField(row.First.Label, row.First.Value, leftX, currentY, columnWidth, labelFontSize, valueFontSize, labelLineHeight, valueLineHeight);
                var rightEndY = DrawStackedField(row.Second.Label, row.Second.Value, rightX, currentY, columnWidth, labelFontSize, valueFontSize, labelLineHeight, valueLineHeight);
                currentY = Math.Min(leftEndY, rightEndY) - rowGap;
            }

            _cursorY = topY - blockHeight - SectionGap;
        }

        private float MeasureRepFieldBlockHeight(IReadOnlyList<RepField> fields)
        {
            const float headerHeight = 18f;
            const float innerPad = 10f;
            const float topPad = 10f;
            const float bottomPad = 10f;
            const float columnGap = 14f;
            const float rowGap = 4f;
            const float valueFontSize = 8.7f;
            const float labelLineHeight = 10f;
            const float valueLineHeight = 10f;

            var sectionWidth = PageWidth - (Margin * 2);
            var contentWidth = sectionWidth - (innerPad * 2);
            var columnWidth = (contentWidth - columnGap) / 2f;
            var rows = BuildRepFieldRows(fields);
            var bodyHeight = 0f;

            for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                var row = rows[rowIndex];
                var rowHeight = row.Second is null
                    ? MeasureStackedFieldHeight(row.First.Value, contentWidth, valueFontSize, labelLineHeight, valueLineHeight)
                    : Math.Max(
                        MeasureStackedFieldHeight(row.First.Value, columnWidth, valueFontSize, labelLineHeight, valueLineHeight),
                        MeasureStackedFieldHeight(row.Second.Value, columnWidth, valueFontSize, labelLineHeight, valueLineHeight));
                bodyHeight += rowHeight;
                if (rowIndex < rows.Count - 1)
                {
                    bodyHeight += rowGap;
                }
            }

            return headerHeight + topPad + bodyHeight + bottomPad;
        }

        private void DrawRepTaxBlock(string title, IReadOnlyList<RepTaxRow> rows)
        {
            const float headerHeight = 18f;
            const float innerPad = 10f;
            const float topPad = 8f;
            const float headerRowHeight = 16f;
            const float fontSize = 7.6f;
            const float lineHeight = 9f;

            var sectionWidth = PageWidth - (Margin * 2);
            var contentWidth = sectionWidth - (innerPad * 2);
            var columns = GetRepTaxColumnWidths(contentWidth);
            var topY = _cursorY;
            var blockHeight = MeasureRepTaxBlockHeight(rows);

            _page.FillRectangle(Margin, topY - blockHeight, sectionWidth, blockHeight, PdfColor.White);
            _page.StrokeRectangle(Margin, topY - blockHeight, sectionWidth, blockHeight, new PdfColor(218, 213, 204), 0.8f);
            _page.FillRectangle(Margin, topY - headerHeight, sectionWidth, headerHeight, new PdfColor(248, 247, 243));
            _page.DrawText(title, Margin + 10f, topY - 13f, 9f, PdfFont.Bold, new PdfColor(88, 80, 66));

            var tableX = Margin + innerPad;
            var tableTopY = topY - headerHeight - topPad;
            var headerLabels = new[] { "Tipo", "Base", "Impuesto", "Factor", "Tasa/Cuota", "Importe" };
            var x = tableX;

            _page.FillRectangle(tableX, tableTopY - headerRowHeight + 2f, contentWidth, headerRowHeight, new PdfColor(238, 235, 228));
            for (var columnIndex = 0; columnIndex < columns.Length; columnIndex++)
            {
                _page.DrawText(headerLabels[columnIndex], x + 4f, tableTopY - 10f, 7.2f, PdfFont.Bold, new PdfColor(88, 80, 66));
                x += columns[columnIndex];
            }

            var currentY = tableTopY - headerRowHeight - 2f;
            foreach (var row in rows)
            {
                var values = new[] { row.RowType, row.Base, row.Tax, row.FactorType, row.Rate, row.Amount };
                var wrappedCells = values
                    .Select((value, index) => WrapTextConservatively(value, Math.Max(24f, columns[index] - 8f), fontSize))
                    .ToArray();
                var rowLineCount = wrappedCells.Max(static lines => Math.Max(1, lines.Length));
                var rowHeight = Math.Max(14f, rowLineCount * lineHeight);

                _page.DrawLine(tableX, currentY + 3f, tableX + contentWidth, currentY + 3f, new PdfColor(230, 230, 230), 0.4f);

                x = tableX;
                for (var columnIndex = 0; columnIndex < columns.Length; columnIndex++)
                {
                    DrawWrappedTextLines(wrappedCells[columnIndex], x + 4f, currentY - 7f, fontSize, lineHeight, PdfFont.Regular, new PdfColor(76, 84, 96));
                    x += columns[columnIndex];
                }

                currentY -= rowHeight;
            }

            _cursorY = topY - blockHeight - SectionGap;
        }

        private float MeasureRepTaxBlockHeight(IReadOnlyList<RepTaxRow> rows)
        {
            const float headerHeight = 18f;
            const float innerPad = 10f;
            const float topPad = 8f;
            const float bottomPad = 10f;
            const float headerRowHeight = 16f;
            const float fontSize = 7.6f;
            const float lineHeight = 9f;

            var sectionWidth = PageWidth - (Margin * 2);
            var contentWidth = sectionWidth - (innerPad * 2);
            var columns = GetRepTaxColumnWidths(contentWidth);
            var bodyHeight = headerRowHeight + 2f;

            foreach (var row in rows)
            {
                var values = new[] { row.RowType, row.Base, row.Tax, row.FactorType, row.Rate, row.Amount };
                var rowLineCount = values
                    .Select((value, index) => WrapTextConservatively(value, Math.Max(24f, columns[index] - 8f), fontSize).Length)
                    .DefaultIfEmpty(1)
                    .Max();
                bodyHeight += Math.Max(14f, Math.Max(1, rowLineCount) * lineHeight);
            }

            return headerHeight + topPad + bodyHeight + bottomPad;
        }

        private static float[] GetRepTaxColumnWidths(float contentWidth)
        {
            return [66f, 98f, 78f, 88f, 102f, Math.Max(70f, contentWidth - 432f)];
        }

        private static IReadOnlyList<(RepField First, RepField? Second)> BuildRepFieldRows(IReadOnlyList<RepField> fields)
        {
            var rows = new List<(RepField First, RepField? Second)>();
            for (var index = 0; index < fields.Count; index++)
            {
                var field = fields[index];
                if (field.PreferFullWidth)
                {
                    rows.Add((field, null));
                    continue;
                }

                if (index + 1 < fields.Count && !fields[index + 1].PreferFullWidth)
                {
                    rows.Add((field, fields[index + 1]));
                    index++;
                    continue;
                }

                rows.Add((field, null));
            }

            return rows;
        }

        private void DrawTotals(PdfViewModel model)
        {
            const float headerHeight = 18f;
            const float innerPad = 8f;
            const float bottomPad = 10f;
            const float wordsFontSize = 9.5f;
            const float wordsLineHeight = 11f;
            const float fieldLabelFontSize = 9f;
            const float fieldValueFontSize = 9f;
            const float fieldLabelLineHeight = 10f;
            const float fieldValueLineHeight = 10f;
            const float taxFontSize = 8f;
            const float taxLineHeight = 10f;

            var leftWidth = 306f;
            var rightWidth = 232f;
            var leftContentWidth = leftWidth - (innerPad * 2);
            var wordsLines = WrapTextConservatively(model.TotalInWords, leftContentWidth, wordsFontSize, isBold: true);
            var wordsHeight = Math.Max(wordsLineHeight, wordsLines.Length * wordsLineHeight);
            var summaryFields = new List<(string Label, string Value)>();
            if (!string.IsNullOrWhiteSpace(model.PaymentForm))
            {
                summaryFields.Add(("Forma de pago", model.PaymentForm));
            }

            if (!string.IsNullOrWhiteSpace(model.PaymentMethod))
            {
                summaryFields.Add(("Metodo de pago", model.PaymentMethod));
            }

            summaryFields.Add(("Exportacion", model.ExportCode));
            summaryFields.Add(("Moneda", model.Currency));

            var fieldHeight = summaryFields.Sum(field => MeasureStackedFieldHeight(field.Value, leftContentWidth, fieldValueFontSize, fieldLabelLineHeight, fieldValueLineHeight));
            var taxLines = model.TaxesBreakdown.Take(2).Select(x => $"{x.Label} {x.Rate}: {x.Amount}").ToArray();
            var taxHeight = taxLines.Length * taxLineHeight;
            var leftHeight = headerHeight + 8f + wordsHeight + 6f + fieldHeight + (taxLines.Length > 0 ? 4f + taxHeight : 0f) + bottomPad;

            var totalsHeight = 84f;
            var sectionHeight = Math.Max(leftHeight, totalsHeight);

            EnsureSpace(sectionHeight + SectionGap);

            var summaryY = _cursorY;

            _page.FillRectangle(Margin, summaryY - leftHeight, leftWidth, leftHeight, PdfColor.White);
            _page.StrokeRectangle(Margin, summaryY - leftHeight, leftWidth, leftHeight, new PdfColor(218, 213, 204), 0.8f);
            _page.FillRectangle(Margin, summaryY - headerHeight, leftWidth, headerHeight, new PdfColor(238, 235, 228));
            _page.DrawText("Resumen fiscal", Margin + 10f, summaryY - 14f, 10f, PdfFont.Bold, new PdfColor(22, 30, 42));

            var leftX = Margin + innerPad;
            var currentY = summaryY - headerHeight - 8f;
            DrawWrappedTextLines(wordsLines, leftX, currentY, wordsFontSize, wordsLineHeight, PdfFont.Bold, new PdfColor(40, 48, 60));
            currentY -= wordsHeight + 6f;

            foreach (var field in summaryFields)
            {
                currentY = DrawStackedField(field.Label, field.Value, leftX, currentY, leftContentWidth, fieldLabelFontSize, fieldValueFontSize, fieldLabelLineHeight, fieldValueLineHeight);
            }

            if (taxLines.Length > 0)
            {
                currentY -= 2f;
                foreach (var taxLine in taxLines)
                {
                    _page.DrawText(taxLine, leftX, currentY, taxFontSize, PdfFont.Regular, new PdfColor(92, 98, 110));
                    currentY -= taxLineHeight;
                }
            }

            var totalsX = PageWidth - Margin - rightWidth;
            _page.FillRectangle(totalsX, summaryY - totalsHeight, rightWidth, totalsHeight, new PdfColor(248, 247, 243));
            _page.StrokeRectangle(totalsX, summaryY - totalsHeight, rightWidth, totalsHeight, new PdfColor(218, 213, 204), 0.8f);
            DrawRightAlignedKeyValue("Subtotal", model.Subtotal, totalsX + 12f, summaryY - 22f, rightWidth - 24f);
            DrawRightAlignedKeyValue("Descuento", model.Discount, totalsX + 12f, summaryY - 40f, rightWidth - 24f);
            DrawRightAlignedKeyValue("Impuestos", model.Taxes, totalsX + 12f, summaryY - 58f, rightWidth - 24f);
            _page.DrawLine(totalsX + 12f, summaryY - 68f, totalsX + rightWidth - 12f, summaryY - 68f, new PdfColor(200, 194, 184), 0.8f);
            DrawRightAlignedKeyValue("Total", model.Total, totalsX + 12f, summaryY - 82f, rightWidth - 24f, 13f, PdfFont.Bold, new PdfColor(22, 30, 42));

            _cursorY = summaryY - sectionHeight - SectionGap;
        }

        private void DrawTimbre(PdfViewModel model, PdfImageAsset? qr)
        {
            const float qrBoxSize = 92f;
            const float innerPad = 10f;
            const float qrGap = 8f;
            var contentLeftX = Margin + innerPad;
            var contentRightX = PageWidth - Margin - innerPad;

            const float labelFontSize = 8.5f;
            float sealFontSize = 6.2f;
            float sealLineHeight = 7.5f;
            float metaLineH = 10f;

            // --- Construir lista de todos los "bloques" de contenido ---
            // Cada bloque tiene: etiqueta (bold) + valor (regular wrapeado)
            var allBlocks = new List<(string Label, string Value)>();

            if (!string.IsNullOrWhiteSpace(model.SatCertificate))
                allBlocks.Add(("No. serie certificado SAT", model.SatCertificate!));
            if (!string.IsNullOrWhiteSpace(model.IssuerCertificate))
                allBlocks.Add(("No. serie CSD emisor", model.IssuerCertificate!));

            var qrPreviewWidth = PageWidth - (Margin * 2) - (innerPad * 2) - qrBoxSize - qrGap;
            var qrLines = string.IsNullOrWhiteSpace(model.Qr)
                ? []
                : WrapText(model.Qr!, EstimateWrapLength(qrPreviewWidth, 7.5f, PdfFont.Regular)).Take(3).ToArray();
            if (qrLines.Length > 0)
                allBlocks.Add(("Consulta SAT / QR", model.Qr!));

            if (!string.IsNullOrWhiteSpace(model.SatSeal))
                allBlocks.Add(("SELLO DIGITAL DEL SAT", model.SatSeal!));
            if (!string.IsNullOrWhiteSpace(model.CfdiSeal))
                allBlocks.Add(("SELLO DIGITAL DEL CFDI", model.CfdiSeal!));
            if (!string.IsNullOrWhiteSpace(model.OriginalString))
                allBlocks.Add(("CADENA ORIGINAL DEL COMPLEMENTO DE CERTIFICACION DIGITAL DEL SAT", model.OriginalString!));

            var pendingBlocks = new Queue<(string Label, string Value)>(allBlocks);
            var hasPendingQr = qr is not null;

            while (pendingBlocks.Count > 0)
            {
                if (!TryDrawTimbreSectionOnCurrentPage(
                        pendingBlocks,
                        qr,
                        hasPendingQr,
                        qrBoxSize,
                        contentLeftX,
                        contentRightX,
                        qrGap,
                        labelFontSize,
                        sealFontSize,
                        sealLineHeight,
                        metaLineH))
                {
                    StartNewPage();
                    continue;
                }

                if (hasPendingQr)
                {
                    hasPendingQr = false;
                }

                if (pendingBlocks.Count > 0)
                {
                    StartNewPage();
                }
            }
        }

        private bool TryDrawTimbreSectionOnCurrentPage(
            Queue<(string Label, string Value)> pendingBlocks,
            PdfImageAsset? qr,
            bool drawQrOnThisPage,
            float qrBoxSize,
            float contentLeftX,
            float contentRightX,
            float qrGap,
            float labelFontSize,
            float sealFontSize,
            float sealLineHeight,
            float metaLineH)
        {
            var pageBlockList = SelectTimbreBlocksForCurrentPage(
                pendingBlocks,
                drawQrOnThisPage,
                qrBoxSize,
                contentLeftX,
                contentRightX,
                qrGap,
                labelFontSize,
                sealFontSize,
                sealLineHeight,
                metaLineH);

            if (pageBlockList.Count == 0)
            {
                return false;
            }

            var qrBoxX = contentLeftX;
            var qrBoxY = _cursorY - 8f - qrBoxSize;
            var qrFlowStartX = qrBoxX + qrBoxSize + qrGap;
            var qrBottomY = qrBoxY;
            var textStartY = _cursorY - 16f;

            var textBottomY = LayoutTimbreBlocks(
                pageBlockList,
                textStartY,
                contentLeftX,
                qrFlowStartX,
                contentRightX,
                qrBottomY,
                drawQrOnThisPage,
                labelFontSize,
                sealFontSize,
                sealLineHeight,
                metaLineH,
                draw: false);

            var sectionBottomY = Math.Min(drawQrOnThisPage ? qrBoxY : textBottomY, textBottomY) - 8f;
            var sectionHeight = _cursorY - sectionBottomY;

            _page.FillRectangle(Margin, _cursorY - sectionHeight, PageWidth - (Margin * 2), sectionHeight, PdfColor.White);
            _page.StrokeRectangle(Margin, _cursorY - sectionHeight, PageWidth - (Margin * 2), sectionHeight, new PdfColor(218, 213, 204), 0.8f);

            if (drawQrOnThisPage && qr is not null)
            {
                _page.FillRectangle(qrBoxX, qrBoxY, qrBoxSize, qrBoxSize, PdfColor.White);
                _page.StrokeRectangle(qrBoxX, qrBoxY, qrBoxSize, qrBoxSize, new PdfColor(218, 213, 204), 0.8f);
                _page.DrawImage(qr, qrBoxX + 6f, qrBoxY + 6f, qrBoxSize - 12f, qrBoxSize - 12f);
            }

            LayoutTimbreBlocks(
                pageBlockList,
                textStartY,
                contentLeftX,
                qrFlowStartX,
                contentRightX,
                qrBottomY,
                drawQrOnThisPage,
                labelFontSize,
                sealFontSize,
                sealLineHeight,
                metaLineH,
                draw: true);

            for (var index = 0; index < pageBlockList.Count; index++)
            {
                pendingBlocks.Dequeue();
            }

            _cursorY = sectionBottomY - SectionGap;
            return true;
        }

        private List<(string Label, string Value)> SelectTimbreBlocksForCurrentPage(
            Queue<(string Label, string Value)> pendingBlocks,
            bool drawQrOnThisPage,
            float qrBoxSize,
            float contentLeftX,
            float contentRightX,
            float qrGap,
            float labelFontSize,
            float sealFontSize,
            float sealLineHeight,
            float metaLineH)
        {
            var pageBlockList = new List<(string Label, string Value)>();
            var previewQueue = new Queue<(string Label, string Value)>(pendingBlocks);
            var qrBoxX = contentLeftX;
            var qrBoxY = _cursorY - 8f - qrBoxSize;
            var qrFlowStartX = qrBoxX + qrBoxSize + qrGap;
            var qrBottomY = qrBoxY;
            var textStartY = _cursorY - 16f;

            while (previewQueue.Count > 0)
            {
                pageBlockList.Add(previewQueue.Dequeue());
                var previewBottomY = LayoutTimbreBlocks(
                    pageBlockList,
                    textStartY,
                    contentLeftX,
                    qrFlowStartX,
                    contentRightX,
                    qrBottomY,
                    drawQrOnThisPage,
                    labelFontSize,
                    sealFontSize,
                    sealLineHeight,
                    metaLineH,
                    draw: false);

                if (previewBottomY < ContentBottom)
                {
                    pageBlockList.RemoveAt(pageBlockList.Count - 1);
                    break;
                }
            }

            return pageBlockList;
        }

        private float LayoutTimbreBlocks(
            IReadOnlyList<(string Label, string Value)> blocks,
            float startY,
            float fullFlowLeftX,
            float qrFlowLeftX,
            float flowRightX,
            float qrBottomY,
            bool hasQr,
            float labelFontSize,
            float sealFontSize,
            float sealLineHeight,
            float metaLineHeight,
            bool draw)
        {
            var currentY = startY;
            foreach (var block in blocks)
            {
                var isSeal = block.Label.StartsWith("SELLO") || block.Label.StartsWith("CADENA");
                var valueFontSize = isSeal ? sealFontSize : 8.5f;
                var lineHeight = isSeal ? sealLineHeight : metaLineHeight;
                currentY = DrawFlowingKeyValueBlock(block.Label, block.Value, currentY, fullFlowLeftX, qrFlowLeftX, flowRightX, qrBottomY, hasQr, labelFontSize, valueFontSize, lineHeight, draw);
                currentY -= 3f;
            }

            return currentY;
        }

        private float DrawFlowingKeyValueBlock(
            string label,
            string value,
            float startY,
            float fullFlowLeftX,
            float qrFlowLeftX,
            float flowRightX,
            float qrBottomY,
            bool hasQr,
            float labelFontSize,
            float valueFontSize,
            float lineHeight,
            bool draw)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return startY;
            }

            const float rightSafetyPadding = 14f;
            const float labelValueGap = 6f;
            var labelText = $"{FormatTimbreLabel(label)}: ";
            var labelWidth = PdfLayoutTextWrapper.EstimateConservativeTextWidth(labelText, labelFontSize, isBold: true) + labelValueGap;
            var remaining = value.Trim();
            var currentY = startY;
            var isFirstLine = true;

            while (remaining.Length > 0)
            {
                var lineLeftX = hasQr && currentY > qrBottomY ? qrFlowLeftX : fullFlowLeftX;
                var lineWidth = Math.Max(20f, flowRightX - lineLeftX - rightSafetyPadding);
                var availableWidth = isFirstLine ? Math.Max(20f, lineWidth - labelWidth) : lineWidth;
                var segment = PdfLayoutTextWrapper.FitTextToWidth(remaining, availableWidth, valueFontSize);
                if (string.IsNullOrEmpty(segment))
                {
                    break;
                }

                if (draw)
                {
                    if (isFirstLine)
                    {
                        _page.DrawText(labelText, lineLeftX, currentY, labelFontSize, PdfFont.Bold, new PdfColor(120, 108, 84));
                        _page.DrawText(segment, lineLeftX + labelWidth, currentY, valueFontSize, PdfFont.Regular, new PdfColor(50, 58, 70));
                    }
                    else
                    {
                        _page.DrawText(segment, lineLeftX, currentY, valueFontSize, PdfFont.Regular, new PdfColor(50, 58, 70));
                    }
                }

                remaining = remaining[segment.Length..].TrimStart();
                currentY -= lineHeight;
                isFirstLine = false;
            }

            return currentY - 2f;
        }

        private float DrawInlineKeyValueParagraph(string label, string value, float x, float y, float maxWidth, float fontSize, float lineHeight)
        {
            var labelText = $"{label}: ";
            var labelWidth = EstimateTextWidth(labelText, fontSize, PdfFont.Bold);
            _page.DrawText(labelText, x, y, fontSize, PdfFont.Bold, new PdfColor(120, 108, 84));

            var availableWidth = maxWidth - labelWidth - 2f;
            if (availableWidth < 10) availableWidth = 10;
            var lines = WrapText(value, EstimateWrapLength(availableWidth, fontSize, PdfFont.Regular)).ToArray();

            if (lines.Length == 0)
            {
                return y - lineHeight;
            }

            _page.DrawText(lines[0], x + labelWidth, y, fontSize, PdfFont.Regular, new PdfColor(50, 58, 70));

            var currentY = y - lineHeight;
            for (int i = 1; i < lines.Length; i++)
            {
                _page.DrawText(lines[i], x + labelWidth, currentY, fontSize, PdfFont.Regular, new PdfColor(50, 58, 70));
                currentY -= lineHeight;
            }

            return currentY - 2f;
        }

        private float DrawStackedField(string label, string value, float x, float startY, float width, float labelFontSize, float valueFontSize, float labelLineHeight, float valueLineHeight)
        {
            _page.DrawText($"{label}:", x, startY, labelFontSize, PdfFont.Bold, new PdfColor(76, 84, 96));

            var valueLines = WrapTextConservatively(value, width, valueFontSize);
            var currentY = startY - labelLineHeight;
            DrawWrappedTextLines(valueLines, x, currentY, valueFontSize, valueLineHeight, PdfFont.Regular, new PdfColor(76, 84, 96));

            return currentY - (valueLines.Length * valueLineHeight) - 4f;
        }

        private static float MeasureStackedFieldHeight(string value, float width, float valueFontSize, float labelLineHeight, float valueLineHeight)
        {
            var lineCount = WrapTextConservatively(value, width, valueFontSize).Length;
            return labelLineHeight + Math.Max(1, lineCount) * valueLineHeight + 4f;
        }

        private void DrawWrappedTextLines(string[] lines, float x, float startY, float fontSize, float lineHeight, PdfFont font, PdfColor color)
        {
            var currentY = startY;
            foreach (var line in lines)
            {
                _page.DrawText(line, x, currentY, fontSize, font, color);
                currentY -= lineHeight;
            }
        }

        private static string[] WrapTextConservatively(string? value, float availableWidth, float fontSize, bool isBold = false)
        {
            var remaining = value?.Trim() ?? string.Empty;
            if (remaining.Length == 0)
            {
                return [string.Empty];
            }

            var lines = new List<string>();
            while (remaining.Length > 0)
            {
                var segment = PdfLayoutTextWrapper.FitTextToWidth(remaining, availableWidth, fontSize, isBold);
                if (string.IsNullOrWhiteSpace(segment))
                {
                    break;
                }

                lines.Add(segment);
                remaining = remaining[segment.Length..].TrimStart();
            }

            return lines.Count == 0 ? [string.Empty] : [.. lines];
        }

        private void DrawFooter(PdfViewModel model)
        {
            var footerY = 28f;
            _page.DrawLine(Margin, footerY, PageWidth - Margin, footerY, new PdfColor(210, 204, 193), 0.7f);
            _page.DrawText("Este documento es una representacion impresa de un CFDI 4.0", Margin, footerY - 12f, 8f, PdfFont.Regular, new PdfColor(92, 98, 110));
            _page.DrawText("PAC / Proveedor: FacturaloPlus", PageWidth - Margin - 180f, footerY - 12f, 8f, PdfFont.Regular, new PdfColor(92, 98, 110));
        }

        private float DrawWrappedKeyValue(string label, string value, float x, float y, float labelWidth, float valueWidth, float labelFontSize, float valueFontSize, float lineHeight)
        {
            var lines = WrapText(value, EstimateWrapLength(valueWidth, valueFontSize, PdfFont.Regular)).ToArray();
            _page.DrawText($"{label}:", x, y, labelFontSize, PdfFont.Bold, new PdfColor(120, 108, 84));
            for (var index = 0; index < lines.Length; index++)
            {
                _page.DrawText(lines[index], x + labelWidth, y - ((index + 1) * lineHeight), valueFontSize, PdfFont.Regular, new PdfColor(50, 58, 70));
            }
            return y - ((lines.Length + 1) * lineHeight) - 4f;
        }

        private void DrawRightAlignedKeyValue(string label, string value, float x, float y, float width, float valueFontSize = 10f, PdfFont? valueFont = null, PdfColor? valueColor = null)
        {
            _page.DrawText(label, x, y, 9f, PdfFont.Bold, new PdfColor(120, 108, 84));
            _page.DrawTextRight(value, x + width, y, valueFontSize, valueFont ?? PdfFont.Regular, valueColor ?? new PdfColor(50, 58, 70));
        }

        private static (float Width, float Height) Fit(int originalWidth, int originalHeight, float maxWidth, float maxHeight)
        {
            var scale = Math.Min(maxWidth / originalWidth, maxHeight / originalHeight);
            return ((float)(originalWidth * scale), (float)(originalHeight * scale));
        }
    }

    private sealed class PdfPageBuilder
    {
        private readonly float _pageWidth;
        private readonly float _pageHeight;
        private readonly StringBuilder _content = new();
        private readonly List<PdfImageAsset> _images = [];

        public PdfPageBuilder(float pageWidth, float pageHeight)
        {
            _pageWidth = pageWidth;
            _pageHeight = pageHeight;
        }

        public void FillRectangle(float x, float y, float width, float height, PdfColor color)
        {
            _content.AppendLine($"{color.FillCommand} rg");
            _content.AppendLine($"{Fmt(x)} {Fmt(y)} {Fmt(width)} {Fmt(height)} re f");
        }

        public void StrokeRectangle(float x, float y, float width, float height, PdfColor color, float lineWidth)
        {
            _content.AppendLine($"{Fmt(lineWidth)} w");
            _content.AppendLine($"{color.StrokeCommand} RG");
            _content.AppendLine($"{Fmt(x)} {Fmt(y)} {Fmt(width)} {Fmt(height)} re S");
        }

        public void DrawLine(float x1, float y1, float x2, float y2, PdfColor color, float lineWidth)
        {
            _content.AppendLine($"{Fmt(lineWidth)} w");
            _content.AppendLine($"{color.StrokeCommand} RG");
            _content.AppendLine($"{Fmt(x1)} {Fmt(y1)} m {Fmt(x2)} {Fmt(y2)} l S");
        }

        public void DrawText(string text, float x, float y, float fontSize, PdfFont font, PdfColor color)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            _content.AppendLine("BT");
            _content.AppendLine($"/{font.ResourceName} {Fmt(fontSize)} Tf");
            _content.AppendLine($"{color.FillCommand} rg");
            _content.AppendLine($"{Fmt(x)} {Fmt(y)} Td");
            _content.AppendLine($"({EscapePdfString(NormalizePdfText(text))}) Tj");
            _content.AppendLine("ET");
        }

        public void DrawTextRight(string text, float x, float y, float fontSize, PdfFont font, PdfColor color)
        {
            var width = EstimateTextWidth(text, fontSize, font);
            DrawText(text, x - width, y, fontSize, font, color);
        }

        public void DrawTextCentered(string text, float leftX, float rightX, float y, float fontSize, PdfFont font, PdfColor color)
        {
            var width = EstimateTextWidth(text, fontSize, font);
            var x = leftX + Math.Max(0f, ((rightX - leftX) - width) / 2f);
            DrawText(text, x, y, fontSize, font, color);
        }

        public void DrawImage(PdfImageAsset image, float x, float y, float width, float height)
        {
            _images.Add(image);
            var resourceName = $"Im{_images.Count}";
            _content.AppendLine("q");
            _content.AppendLine($"{Fmt(width)} 0 0 {Fmt(height)} {Fmt(x)} {Fmt(y)} cm");
            _content.AppendLine($"/{resourceName} Do");
            _content.AppendLine("Q");
        }

        public static byte[] BuildDocument(IReadOnlyList<PdfPageBuilder> pages)
        {
            var objects = new List<PdfObject>();
            var nextId = 1;
            var catalogId = nextId++;
            var pagesId = nextId++;
            var regularFontId = nextId++;
            var boldFontId = nextId++;

            objects.Add(PdfObject.FromText(catalogId, $"<< /Type /Catalog /Pages {pagesId} 0 R >>"));
            objects.Add(PdfObject.FromText(regularFontId, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"));
            objects.Add(PdfObject.FromText(boldFontId, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>"));

            var pageIds = new List<int>();
            foreach (var page in pages)
            {
                var imageIds = new List<int>();
                foreach (var image in page._images)
                {
                    var imageId = nextId++;
                    imageIds.Add(imageId);
                    objects.Add(image.BuildObject(imageId));
                }

                var contentId = nextId++;
                var contentText = page._content.ToString();
                objects.Add(PdfObject.FromText(contentId, $"<< /Length {Encoding.ASCII.GetByteCount(contentText)} >>\nstream\n{contentText}\nendstream"));

                var xObjectEntries = imageIds.Count == 0
                    ? string.Empty
                    : $"/XObject << {string.Join(' ', imageIds.Select((id, index) => $"/Im{index + 1} {id} 0 R"))} >>";

                var pageId = nextId++;
                pageIds.Add(pageId);
                objects.Add(PdfObject.FromText(
                    pageId,
                    $"<< /Type /Page /Parent {pagesId} 0 R /MediaBox [0 0 {Fmt(page._pageWidth)} {Fmt(page._pageHeight)}] /Resources << /Font << /F1 {regularFontId} 0 R /F2 {boldFontId} 0 R >> {xObjectEntries} >> /Contents {contentId} 0 R >>"));
            }

            objects.Add(PdfObject.FromText(pagesId, $"<< /Type /Pages /Count {pageIds.Count} /Kids [{string.Join(' ', pageIds.Select(id => $"{id} 0 R"))}] >>"));

            objects.Sort((left, right) => left.Id.CompareTo(right.Id));
            return PdfSerializer.Serialize(objects);
        }
    }

    private sealed class PdfObject
    {
        public required int Id { get; init; }
        public required byte[] BodyBytes { get; init; }

        public static PdfObject FromText(int id, string body)
        {
            return new PdfObject
            {
                Id = id,
                BodyBytes = Encoding.ASCII.GetBytes(body)
            };
        }
    }

    private static class PdfSerializer
    {
        public static byte[] Serialize(IReadOnlyList<PdfObject> objects)
        {
            using var stream = new MemoryStream();
            WriteAscii(stream, "%PDF-1.4\n");
            var offsets = new List<int> { 0 };

            foreach (var pdfObject in objects)
            {
                offsets.Add((int)stream.Position);
                WriteAscii(stream, $"{pdfObject.Id} 0 obj\n");
                stream.Write(pdfObject.BodyBytes, 0, pdfObject.BodyBytes.Length);
                WriteAscii(stream, "\nendobj\n");
            }

            var xrefPosition = (int)stream.Position;
            WriteAscii(stream, $"xref\n0 {objects.Count + 1}\n");
            WriteAscii(stream, "0000000000 65535 f \n");

            foreach (var offset in offsets.Skip(1))
            {
                WriteAscii(stream, $"{offset.ToString("0000000000", CultureInfo.InvariantCulture)} 00000 n \n");
            }

            WriteAscii(stream, $"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\n");
            WriteAscii(stream, $"startxref\n{xrefPosition.ToString(CultureInfo.InvariantCulture)}\n%%EOF");
            return stream.ToArray();
        }

        private static void WriteAscii(Stream stream, string value)
        {
            var bytes = Encoding.ASCII.GetBytes(value);
            stream.Write(bytes, 0, bytes.Length);
        }
    }

    private sealed class PdfImageAsset
    {
        public required int Width { get; init; }
        public required int Height { get; init; }
        public required string Filter { get; init; }
        public required string ColorSpace { get; init; }
        public required byte[] Data { get; init; }

        public static PdfImageAsset? TryCreate(byte[] content, string? contentType)
        {
            if (content.Length == 0)
            {
                return null;
            }

            if (contentType?.Contains("jpeg", StringComparison.OrdinalIgnoreCase) == true
                || content.AsSpan().StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }))
            {
                return TryCreateJpeg(content);
            }

            if (contentType?.Contains("png", StringComparison.OrdinalIgnoreCase) == true
                || content.AsSpan().StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47 }))
            {
                return TryCreatePng(content);
            }

            return null;
        }

        public static PdfImageAsset CreateRgb(int width, int height, byte[] rgbData)
        {
            using var output = new MemoryStream();
            using (var flate = new ZLibStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
            {
                flate.Write(rgbData, 0, rgbData.Length);
            }

            return new PdfImageAsset
            {
                Width = width,
                Height = height,
                Filter = "FlateDecode",
                ColorSpace = "DeviceRGB",
                Data = output.ToArray()
            };
        }

        public PdfObject BuildObject(int id)
        {
            var header = Encoding.ASCII.GetBytes($"<< /Type /XObject /Subtype /Image /Width {Width} /Height {Height} /ColorSpace /{ColorSpace} /BitsPerComponent 8 /Filter /{Filter} /Length {Data.Length} >>\nstream\n");
            var footer = Encoding.ASCII.GetBytes("\nendstream");
            var body = new byte[header.Length + Data.Length + footer.Length];
            Buffer.BlockCopy(header, 0, body, 0, header.Length);
            Buffer.BlockCopy(Data, 0, body, header.Length, Data.Length);
            Buffer.BlockCopy(footer, 0, body, header.Length + Data.Length, footer.Length);
            return new PdfObject
            {
                Id = id,
                BodyBytes = body
            };
        }

        private static PdfImageAsset? TryCreateJpeg(byte[] content)
        {
            if (!TryReadJpegSize(content, out var width, out var height))
            {
                return null;
            }

            return new PdfImageAsset
            {
                Width = width,
                Height = height,
                Filter = "DCTDecode",
                ColorSpace = "DeviceRGB",
                Data = content
            };
        }

        private static PdfImageAsset? TryCreatePng(byte[] content)
        {
            try
            {
                return PngPdfImageDecoder.Decode(content);
            }
            catch
            {
                return null;
            }
        }

        private static bool TryReadJpegSize(byte[] bytes, out int width, out int height)
        {
            width = 0;
            height = 0;
            var index = 2;
            while (index < bytes.Length - 9)
            {
                if (bytes[index] != 0xFF)
                {
                    index++;
                    continue;
                }

                var marker = bytes[index + 1];
                if (marker is 0xC0 or 0xC1 or 0xC2 or 0xC3)
                {
                    height = (bytes[index + 5] << 8) + bytes[index + 6];
                    width = (bytes[index + 7] << 8) + bytes[index + 8];
                    return true;
                }

                var segmentLength = (bytes[index + 2] << 8) + bytes[index + 3];
                if (segmentLength <= 0)
                {
                    break;
                }

                index += 2 + segmentLength;
            }

            return false;
        }
    }

    private static class PngPdfImageDecoder
    {
        public static PdfImageAsset Decode(byte[] content)
        {
            using var stream = new MemoryStream(content);
            using var reader = new BinaryReader(stream);

            var signature = reader.ReadBytes(8);
            if (!signature.SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            {
                throw new InvalidOperationException("Invalid PNG signature.");
            }

            var width = 0;
            var height = 0;
            byte bitDepth = 0;
            byte colorType = 0;
            byte interlaceMethod = 0;
            using var idat = new MemoryStream();

            while (stream.Position < stream.Length)
            {
                var length = ReadInt32BigEndian(reader);
                var chunkType = Encoding.ASCII.GetString(reader.ReadBytes(4));
                var chunkData = reader.ReadBytes(length);
                _ = reader.ReadUInt32();

                switch (chunkType)
                {
                    case "IHDR":
                        width = ReadInt32BigEndian(chunkData, 0);
                        height = ReadInt32BigEndian(chunkData, 4);
                        bitDepth = chunkData[8];
                        colorType = chunkData[9];
                        interlaceMethod = chunkData[12];
                        break;
                    case "IDAT":
                        idat.Write(chunkData);
                        break;
                    case "IEND":
                        stream.Position = stream.Length;
                        break;
                }
            }

            if (width <= 0 || height <= 0 || bitDepth != 8 || interlaceMethod != 0)
            {
                throw new InvalidOperationException("Unsupported PNG format.");
            }

            using var compressed = new MemoryStream(idat.ToArray());
            using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
            using var decompressed = new MemoryStream();
            zlib.CopyTo(decompressed);
            var scanlines = decompressed.ToArray();

            var channels = colorType switch
            {
                2 => 3,
                6 => 4,
                _ => throw new InvalidOperationException("Unsupported PNG color type.")
            };

            var stride = width * channels;
            var raw = new byte[height * stride];
            var previous = new byte[stride];
            var offset = 0;
            var rawOffset = 0;

            for (var row = 0; row < height; row++)
            {
                var filter = scanlines[offset++];
                var current = new byte[stride];
                Array.Copy(scanlines, offset, current, 0, stride);
                offset += stride;
                Unfilter(filter, current, previous, channels);
                Array.Copy(current, 0, raw, rawOffset, stride);
                rawOffset += stride;
                previous = current;
            }

            var rgb = colorType == 2 ? raw : FlattenRgbaOnWhite(raw);
            using var output = new MemoryStream();
            using (var flate = new ZLibStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
            {
                flate.Write(rgb, 0, rgb.Length);
            }

            return new PdfImageAsset
            {
                Width = width,
                Height = height,
                Filter = "FlateDecode",
                ColorSpace = "DeviceRGB",
                Data = output.ToArray()
            };
        }

        private static void Unfilter(byte filter, byte[] current, byte[] previous, int bytesPerPixel)
        {
            switch (filter)
            {
                case 0:
                    return;
                case 1:
                    for (var i = bytesPerPixel; i < current.Length; i++)
                    {
                        current[i] = (byte)(current[i] + current[i - bytesPerPixel]);
                    }

                    return;
                case 2:
                    for (var i = 0; i < current.Length; i++)
                    {
                        current[i] = (byte)(current[i] + previous[i]);
                    }

                    return;
                case 3:
                    for (var i = 0; i < current.Length; i++)
                    {
                        var left = i >= bytesPerPixel ? current[i - bytesPerPixel] : 0;
                        var up = previous[i];
                        current[i] = (byte)(current[i] + ((left + up) / 2));
                    }

                    return;
                case 4:
                    for (var i = 0; i < current.Length; i++)
                    {
                        var left = i >= bytesPerPixel ? current[i - bytesPerPixel] : 0;
                        var up = previous[i];
                        var upperLeft = i >= bytesPerPixel ? previous[i - bytesPerPixel] : 0;
                        current[i] = (byte)(current[i] + PaethPredictor(left, up, upperLeft));
                    }

                    return;
                default:
                    throw new InvalidOperationException("Unsupported PNG filter.");
            }
        }

        private static byte[] FlattenRgbaOnWhite(byte[] rgba)
        {
            var rgb = new byte[(rgba.Length / 4) * 3];
            var target = 0;

            for (var index = 0; index < rgba.Length; index += 4)
            {
                var alpha = rgba[index + 3] / 255f;
                rgb[target++] = BlendOnWhite(rgba[index], alpha);
                rgb[target++] = BlendOnWhite(rgba[index + 1], alpha);
                rgb[target++] = BlendOnWhite(rgba[index + 2], alpha);
            }

            return rgb;
        }

        private static byte BlendOnWhite(byte component, float alpha)
        {
            return (byte)Math.Round((component * alpha) + (255f * (1f - alpha)), MidpointRounding.AwayFromZero);
        }

        private static int PaethPredictor(int left, int up, int upperLeft)
        {
            var prediction = left + up - upperLeft;
            var distanceLeft = Math.Abs(prediction - left);
            var distanceUp = Math.Abs(prediction - up);
            var distanceUpperLeft = Math.Abs(prediction - upperLeft);

            if (distanceLeft <= distanceUp && distanceLeft <= distanceUpperLeft)
            {
                return left;
            }

            return distanceUp <= distanceUpperLeft ? up : upperLeft;
        }

        private static int ReadInt32BigEndian(BinaryReader reader)
        {
            var bytes = reader.ReadBytes(4);
            return ReadInt32BigEndian(bytes, 0);
        }

        private static int ReadInt32BigEndian(byte[] bytes, int offset)
        {
            return (bytes[offset] << 24)
                 | (bytes[offset + 1] << 16)
                 | (bytes[offset + 2] << 8)
                 | bytes[offset + 3];
        }
    }

    private sealed record PdfColor(byte R, byte G, byte B)
    {
        public static PdfColor White => new(255, 255, 255);

        public string FillCommand => $"{Fmt(R / 255f)} {Fmt(G / 255f)} {Fmt(B / 255f)}";
        public string StrokeCommand => FillCommand;
    }

    private sealed record PdfFont(string ResourceName)
    {
        public static PdfFont Regular => new("F1");
        public static PdfFont Bold => new("F2");
    }

    private static IEnumerable<string> WrapText(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var lines = new List<string>();
        var remaining = value.Trim();
        while (remaining.Length > maxLength)
        {
            var splitIndex = remaining.LastIndexOf(' ', maxLength);
            if (splitIndex <= 0)
            {
                splitIndex = maxLength;
            }

            lines.Add(remaining[..splitIndex].TrimEnd());
            remaining = remaining[splitIndex..].TrimStart();
        }

        lines.Add(remaining);
        return lines;
    }

    private static float EstimateTextWidth(string text, float fontSize, PdfFont font)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0f;
        }

        var multiplier = font == PdfFont.Bold ? 0.56f : 0.52f;
        return NormalizePdfText(text).Length * fontSize * multiplier;
    }

    private static int EstimateWrapLength(float width, float fontSize, PdfFont font)
    {
        var averageCharacterWidth = fontSize * (font == PdfFont.Bold ? 0.56f : 0.52f);
        return Math.Max(6, (int)Math.Floor(width / averageCharacterWidth));
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

    private static string Fmt(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
