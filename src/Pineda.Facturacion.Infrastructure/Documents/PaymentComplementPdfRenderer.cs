using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Pineda.Facturacion.Application.Abstractions.Documents;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.Documents;

public sealed class PaymentComplementPdfRenderer : IPaymentComplementPdfRenderer
{
    private readonly IFiscalDocumentPdfRenderer _fiscalDocumentPdfRenderer;

    public PaymentComplementPdfRenderer(IFiscalDocumentPdfRenderer fiscalDocumentPdfRenderer)
    {
        _fiscalDocumentPdfRenderer = fiscalDocumentPdfRenderer;
    }

    public async Task<byte[]> RenderAsync(
        PaymentComplementDocument paymentComplementDocument,
        PaymentComplementStamp paymentComplementStamp,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paymentComplementDocument);
        ArgumentNullException.ThrowIfNull(paymentComplementStamp);

        if (string.IsNullOrWhiteSpace(paymentComplementStamp.XmlContent))
        {
            throw new InvalidOperationException("Stamped XML is required to build the payment complement PDF.");
        }

        XDocument document;
        try
        {
            document = XDocument.Parse(paymentComplementStamp.XmlContent, LoadOptions.PreserveWhitespace);
        }
        catch (Exception exception) when (exception is XmlException or InvalidOperationException)
        {
            throw new InvalidOperationException("Stamped XML could not be parsed to build the payment complement PDF.", exception);
        }

        var surrogateFiscalDocument = BuildSurrogateFiscalDocument(paymentComplementDocument, document);
        var surrogateFiscalStamp = BuildSurrogateFiscalStamp(paymentComplementStamp);
        return await _fiscalDocumentPdfRenderer.RenderAsync(surrogateFiscalDocument, surrogateFiscalStamp, cancellationToken);
    }

    private static FiscalDocument BuildSurrogateFiscalDocument(PaymentComplementDocument paymentComplementDocument, XDocument stampedXml)
    {
        var comprobante = stampedXml.Descendants().FirstOrDefault(static node => node.Name.LocalName == "Comprobante");
        var receptor = stampedXml.Descendants().FirstOrDefault(static node => node.Name.LocalName == "Receptor");
        var pagos = stampedXml.Descendants().FirstOrDefault(static node => node.Name.LocalName == "Pagos");
        var firstPago = stampedXml.Descendants().FirstOrDefault(static node => node.Name.LocalName == "Pago");

        return new FiscalDocument
        {
            Id = paymentComplementDocument.Id,
            BillingDocumentId = 0,
            IssuerProfileId = paymentComplementDocument.IssuerProfileId ?? 0,
            FiscalReceiverId = paymentComplementDocument.FiscalReceiverId ?? 0,
            CfdiVersion = paymentComplementDocument.CfdiVersion,
            DocumentType = paymentComplementDocument.DocumentType,
            Series = GetAttribute(comprobante, "Serie"),
            Folio = GetAttribute(comprobante, "Folio"),
            IssuedAtUtc = paymentComplementDocument.IssuedAtUtc,
            CurrencyCode = GetAttribute(comprobante, "Moneda") ?? "XXX",
            ExchangeRate = ParseNullableDecimal(GetAttribute(comprobante, "TipoCambio")),
            PaymentMethodSat = string.Empty,
            PaymentFormSat = string.Empty,
            PaymentCondition = null,
            IsCreditSale = false,
            CreditDays = null,
            Status = paymentComplementDocument.Status == Domain.Enums.PaymentComplementDocumentStatus.Cancelled
                ? Domain.Enums.FiscalDocumentStatus.Cancelled
                : Domain.Enums.FiscalDocumentStatus.Stamped,
            IssuerRfc = paymentComplementDocument.IssuerRfc,
            IssuerLegalName = paymentComplementDocument.IssuerLegalName,
            IssuerFiscalRegimeCode = paymentComplementDocument.IssuerFiscalRegimeCode,
            IssuerPostalCode = paymentComplementDocument.IssuerPostalCode,
            PacEnvironment = paymentComplementDocument.PacEnvironment,
            CertificateReference = paymentComplementDocument.CertificateReference,
            PrivateKeyReference = paymentComplementDocument.PrivateKeyReference,
            PrivateKeyPasswordReference = paymentComplementDocument.PrivateKeyPasswordReference,
            ReceiverRfc = paymentComplementDocument.ReceiverRfc,
            ReceiverLegalName = paymentComplementDocument.ReceiverLegalName,
            ReceiverFiscalRegimeCode = paymentComplementDocument.ReceiverFiscalRegimeCode,
            ReceiverCfdiUseCode = GetAttribute(receptor, "UsoCFDI") ?? "CP01",
            ReceiverPostalCode = paymentComplementDocument.ReceiverPostalCode,
            ReceiverCountryCode = paymentComplementDocument.ReceiverCountryCode,
            ReceiverForeignTaxRegistration = paymentComplementDocument.ReceiverForeignTaxRegistration,
            Subtotal = ParseDecimal(GetAttribute(comprobante, "SubTotal"), 0m),
            DiscountTotal = ParseDecimal(GetAttribute(comprobante, "Descuento"), 0m),
            TaxTotal = 0m,
            Total = ParseDecimal(GetAttribute(comprobante, "Total"), 0m),
            CreatedAtUtc = paymentComplementDocument.CreatedAtUtc,
            UpdatedAtUtc = paymentComplementDocument.UpdatedAtUtc,
            SpecialFieldValues = BuildSpecialFields(paymentComplementDocument.Id, stampedXml, paymentComplementDocument.CreatedAtUtc)
        };
    }

    private static FiscalStamp BuildSurrogateFiscalStamp(PaymentComplementStamp paymentComplementStamp)
    {
        return new FiscalStamp
        {
            Id = paymentComplementStamp.Id,
            FiscalDocumentId = paymentComplementStamp.PaymentComplementDocumentId,
            ProviderName = paymentComplementStamp.ProviderName,
            ProviderOperation = paymentComplementStamp.ProviderOperation,
            Status = paymentComplementStamp.Status,
            ProviderRequestHash = paymentComplementStamp.ProviderRequestHash,
            ProviderTrackingId = paymentComplementStamp.ProviderTrackingId,
            ProviderCode = paymentComplementStamp.ProviderCode,
            ProviderMessage = paymentComplementStamp.ProviderMessage,
            Uuid = paymentComplementStamp.Uuid,
            StampedAtUtc = paymentComplementStamp.StampedAtUtc,
            XmlContent = paymentComplementStamp.XmlContent,
            XmlHash = paymentComplementStamp.XmlHash,
            OriginalString = paymentComplementStamp.OriginalString,
            QrCodeTextOrUrl = paymentComplementStamp.QrCodeTextOrUrl,
            RawResponseSummaryJson = paymentComplementStamp.RawResponseSummaryJson,
            ErrorCode = paymentComplementStamp.ErrorCode,
            ErrorMessage = paymentComplementStamp.ErrorMessage,
            CreatedAtUtc = paymentComplementStamp.CreatedAtUtc,
            UpdatedAtUtc = paymentComplementStamp.UpdatedAtUtc
        };
    }

    private static List<FiscalDocumentSpecialFieldValue> BuildSpecialFields(long fiscalDocumentId, XDocument stampedXml, DateTime createdAtUtc)
    {
        var fields = new List<FiscalDocumentSpecialFieldValue>();
        var displayOrder = 1;
        var pagos = stampedXml.Descendants().FirstOrDefault(static node => node.Name.LocalName == "Pagos");
        var totales = pagos?.Elements().FirstOrDefault(static node => node.Name.LocalName == "Totales");
        var pagosNodes = pagos?.Elements().Where(static node => node.Name.LocalName == "Pago").ToList() ?? [];

        AddField(fields, fiscalDocumentId, ref displayOrder, "REP_COMPLEMENT_FIELD_VERSION", "Version Pagos 2.0", GetAttribute(pagos, "Version"), createdAtUtc);
        AddField(fields, fiscalDocumentId, ref displayOrder, "REP_COMPLEMENT_FIELD_MONTOTOTALPAGOS", "Monto total de pagos", GetAttribute(totales, "MontoTotalPagos"), createdAtUtc);

        if (totales is not null)
        {
            foreach (var attribute in totales.Attributes().OrderBy(static attribute => attribute.Name.LocalName, StringComparer.Ordinal))
            {
                if (string.Equals(attribute.Name.LocalName, "MontoTotalPagos", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                AddField(
                    fields,
                    fiscalDocumentId,
                    ref displayOrder,
                    $"REP_COMPLEMENT_FIELD_{NormalizeFieldCodeToken(attribute.Name.LocalName)}",
                    GetComplementFieldLabel(attribute.Name.LocalName),
                    attribute.Value,
                    createdAtUtc);
            }
        }

        for (var paymentIndex = 0; paymentIndex < pagosNodes.Count; paymentIndex++)
        {
            var pago = pagosNodes[paymentIndex];
            var paymentNumber = paymentIndex + 1;
            AddField(fields, fiscalDocumentId, ref displayOrder, $"REP_PAYMENT_{paymentNumber}_FIELD_FECHAPAGO", "Fecha de pago", GetAttribute(pago, "FechaPago"), createdAtUtc);
            AddField(fields, fiscalDocumentId, ref displayOrder, $"REP_PAYMENT_{paymentNumber}_FIELD_FORMADEPAGOP", "Forma de pago", GetAttribute(pago, "FormaDePagoP"), createdAtUtc);
            AddField(fields, fiscalDocumentId, ref displayOrder, $"REP_PAYMENT_{paymentNumber}_FIELD_MONEDAP", "Moneda", GetAttribute(pago, "MonedaP"), createdAtUtc);
            AddField(fields, fiscalDocumentId, ref displayOrder, $"REP_PAYMENT_{paymentNumber}_FIELD_TIPOCAMBIOP", "Tipo de cambio", GetAttribute(pago, "TipoCambioP"), createdAtUtc);
            AddField(fields, fiscalDocumentId, ref displayOrder, $"REP_PAYMENT_{paymentNumber}_FIELD_MONTO", "Monto", GetAttribute(pago, "Monto"), createdAtUtc);
            AddField(fields, fiscalDocumentId, ref displayOrder, $"REP_PAYMENT_{paymentNumber}_FIELD_NUMOPERACION", "Numero de operacion", GetAttribute(pago, "NumOperacion"), createdAtUtc);

            AddTaxFields(
                fields,
                fiscalDocumentId,
                ref displayOrder,
                $"REP_PAYMENT_{paymentNumber}",
                pago,
                "ImpuestosP",
                "TrasladoP",
                "RetencionP",
                createdAtUtc);

            var relatedDocuments = pago.Elements().Where(static node => node.Name.LocalName == "DoctoRelacionado").ToList();
            for (var documentIndex = 0; documentIndex < relatedDocuments.Count; documentIndex++)
            {
                var relatedDocument = relatedDocuments[documentIndex];
                var documentNumber = documentIndex + 1;
                var fieldCodePrefix = $"REP_PAYMENT_{paymentNumber}_DOCUMENT_{documentNumber}";
                AddField(fields, fiscalDocumentId, ref displayOrder, $"{fieldCodePrefix}_FIELD_IDDOCUMENTO", "UUID documento", GetAttribute(relatedDocument, "IdDocumento"), createdAtUtc);
                AddField(fields, fiscalDocumentId, ref displayOrder, $"{fieldCodePrefix}_FIELD_SERIE", "Serie", GetAttribute(relatedDocument, "Serie"), createdAtUtc);
                AddField(fields, fiscalDocumentId, ref displayOrder, $"{fieldCodePrefix}_FIELD_FOLIO", "Folio", GetAttribute(relatedDocument, "Folio"), createdAtUtc);
                AddField(fields, fiscalDocumentId, ref displayOrder, $"{fieldCodePrefix}_FIELD_MONEDADR", "Moneda del documento", GetAttribute(relatedDocument, "MonedaDR"), createdAtUtc);
                AddField(fields, fiscalDocumentId, ref displayOrder, $"{fieldCodePrefix}_FIELD_EQUIVALENCIADR", "Equivalencia", GetAttribute(relatedDocument, "EquivalenciaDR"), createdAtUtc);
                AddField(fields, fiscalDocumentId, ref displayOrder, $"{fieldCodePrefix}_FIELD_NUMPARCIALIDAD", "Numero de parcialidad", GetAttribute(relatedDocument, "NumParcialidad"), createdAtUtc);
                AddField(fields, fiscalDocumentId, ref displayOrder, $"{fieldCodePrefix}_FIELD_IMPSALDOANT", "Saldo anterior", GetAttribute(relatedDocument, "ImpSaldoAnt"), createdAtUtc);
                AddField(fields, fiscalDocumentId, ref displayOrder, $"{fieldCodePrefix}_FIELD_IMPPAGADO", "Monto pagado", GetAttribute(relatedDocument, "ImpPagado"), createdAtUtc);
                AddField(fields, fiscalDocumentId, ref displayOrder, $"{fieldCodePrefix}_FIELD_IMPSALDOINSOLUTO", "Saldo insoluto", GetAttribute(relatedDocument, "ImpSaldoInsoluto"), createdAtUtc);
                AddField(fields, fiscalDocumentId, ref displayOrder, $"{fieldCodePrefix}_FIELD_OBJETOIMPDR", "Objeto de impuesto", GetAttribute(relatedDocument, "ObjetoImpDR"), createdAtUtc);

                AddTaxFields(
                    fields,
                    fiscalDocumentId,
                    ref displayOrder,
                    fieldCodePrefix,
                    relatedDocument,
                    "ImpuestosDR",
                    "TrasladoDR",
                    "RetencionDR",
                    createdAtUtc);
            }
        }

        return fields;
    }

    private static void AddTaxFields(
        List<FiscalDocumentSpecialFieldValue> fields,
        long fiscalDocumentId,
        ref int displayOrder,
        string fieldCodePrefix,
        XElement scope,
        string taxesContainerName,
        string transferName,
        string retentionName,
        DateTime createdAtUtc)
    {
        var taxesContainer = scope.Elements().FirstOrDefault(node => string.Equals(node.Name.LocalName, taxesContainerName, StringComparison.OrdinalIgnoreCase));
        if (taxesContainer is null)
        {
            return;
        }

        var taxSequence = 1;
        foreach (var traslado in taxesContainer.Descendants().Where(node => string.Equals(node.Name.LocalName, transferName, StringComparison.OrdinalIgnoreCase)))
        {
            AddField(
                fields,
                fiscalDocumentId,
                ref displayOrder,
                $"{fieldCodePrefix}_TAX_{NormalizeFieldCodeToken(traslado.Name.LocalName)}_{taxSequence++}",
                "Traslado",
                SummarizeAttributes(traslado),
                createdAtUtc);
        }

        foreach (var retencion in taxesContainer.Descendants().Where(node => string.Equals(node.Name.LocalName, retentionName, StringComparison.OrdinalIgnoreCase)))
        {
            AddField(
                fields,
                fiscalDocumentId,
                ref displayOrder,
                $"{fieldCodePrefix}_TAX_{NormalizeFieldCodeToken(retencion.Name.LocalName)}_{taxSequence++}",
                "Retencion",
                SummarizeAttributes(retencion),
                createdAtUtc);
        }
    }

    private static string? SummarizeAttributes(XElement element)
    {
        var parts = element.Attributes()
            .Select(attribute => $"{attribute.Name.LocalName}={attribute.Value}")
            .ToArray();
        return parts.Length == 0 ? null : string.Join(" | ", parts);
    }

    private static void AddField(
        List<FiscalDocumentSpecialFieldValue> fields,
        long fiscalDocumentId,
        ref int displayOrder,
        string fieldCode,
        string label,
        string? value,
        DateTime createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        fields.Add(new FiscalDocumentSpecialFieldValue
        {
            FiscalDocumentId = fiscalDocumentId,
            FieldCode = fieldCode,
            FieldLabelSnapshot = label,
            DataType = "text",
            Value = value.Trim(),
            DisplayOrder = displayOrder++,
            CreatedAtUtc = createdAtUtc
        });
    }

    private static string GetComplementFieldLabel(string attributeName)
    {
        return attributeName switch
        {
            "Version" => "Version Pagos 2.0",
            "MontoTotalPagos" => "Monto total de pagos",
            "TotalTrasladosBaseIVA16" => "Base trasladada IVA 16%",
            "TotalTrasladosImpuestoIVA16" => "IVA trasladado 16%",
            "TotalRetencionesIVA" => "Retencion IVA",
            "TotalRetencionesISR" => "Retencion ISR",
            "TotalRetencionesIEPS" => "Retencion IEPS",
            _ => HumanizeAttributeName(attributeName)
        };
    }

    private static string NormalizeFieldCodeToken(string value)
    {
        return new string(value
            .Trim()
            .ToUpperInvariant()
            .Where(static character => char.IsLetterOrDigit(character) || character == '_')
            .ToArray());
    }

    private static string HumanizeAttributeName(string attributeName)
    {
        if (string.IsNullOrWhiteSpace(attributeName))
        {
            return string.Empty;
        }

        var buffer = new List<char>(attributeName.Length + 8);
        for (var index = 0; index < attributeName.Length; index++)
        {
            var current = attributeName[index];
            if (index > 0)
            {
                var previous = attributeName[index - 1];
                var next = index + 1 < attributeName.Length ? attributeName[index + 1] : '\0';
                var startsNewWord =
                    (char.IsLower(previous) && char.IsUpper(current))
                    || (char.IsLetter(previous) && char.IsDigit(current))
                    || (char.IsDigit(previous) && char.IsLetter(current))
                    || (char.IsUpper(previous) && char.IsUpper(current) && next != '\0' && char.IsLower(next));

                if (startsNewWord)
                {
                    buffer.Add(' ');
                }
            }

            buffer.Add(current);
        }

        return new string(buffer.ToArray());
    }

    private static string? GetAttribute(XElement? element, string attributeName)
        => element?.Attribute(XName.Get(attributeName))?.Value?.Trim();

    private static decimal ParseDecimal(string? value, decimal fallbackValue)
    {
        return decimal.TryParse(value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallbackValue;
    }

    private static decimal? ParseNullableDecimal(string? value)
    {
        return decimal.TryParse(value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }
}
