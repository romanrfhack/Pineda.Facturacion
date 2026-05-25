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
            PaymentFormSat = GetAttribute(firstPago, "FormaDePagoP") ?? paymentComplementDocument.Payments.OrderBy(x => x.Id).FirstOrDefault()?.PaymentFormSat ?? string.Empty,
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

        AddField(fields, fiscalDocumentId, ref displayOrder, "REP_VERSION", "Complemento Pagos 2.0 - Version", GetAttribute(pagos, "Version"), createdAtUtc);
        AddField(fields, fiscalDocumentId, ref displayOrder, "REP_TOTAL_PAYMENTS", "Complemento Pagos 2.0 - MontoTotalPagos", GetAttribute(totales, "MontoTotalPagos"), createdAtUtc);

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
                    $"REP_TOTAL_{attribute.Name.LocalName.ToUpperInvariant()}",
                    $"Totales - {attribute.Name.LocalName}",
                    attribute.Value,
                    createdAtUtc);
            }
        }

        for (var paymentIndex = 0; paymentIndex < pagosNodes.Count; paymentIndex++)
        {
            var pago = pagosNodes[paymentIndex];
            var paymentLabel = $"Pago {paymentIndex + 1}";
            AddField(fields, fiscalDocumentId, ref displayOrder, $"REP_PAGO_{paymentIndex + 1}_FECHA", $"{paymentLabel} - FechaPago", GetAttribute(pago, "FechaPago"), createdAtUtc);
            AddField(fields, fiscalDocumentId, ref displayOrder, $"REP_PAGO_{paymentIndex + 1}_FORMA", $"{paymentLabel} - FormaDePagoP", GetAttribute(pago, "FormaDePagoP"), createdAtUtc);
            AddField(fields, fiscalDocumentId, ref displayOrder, $"REP_PAGO_{paymentIndex + 1}_MONEDA", $"{paymentLabel} - MonedaP", GetAttribute(pago, "MonedaP"), createdAtUtc);
            AddField(fields, fiscalDocumentId, ref displayOrder, $"REP_PAGO_{paymentIndex + 1}_TIPOCAMBIO", $"{paymentLabel} - TipoCambioP", GetAttribute(pago, "TipoCambioP"), createdAtUtc);
            AddField(fields, fiscalDocumentId, ref displayOrder, $"REP_PAGO_{paymentIndex + 1}_MONTO", $"{paymentLabel} - Monto", GetAttribute(pago, "Monto"), createdAtUtc);
            AddField(fields, fiscalDocumentId, ref displayOrder, $"REP_PAGO_{paymentIndex + 1}_OPERACION", $"{paymentLabel} - NumOperacion", GetAttribute(pago, "NumOperacion"), createdAtUtc);

            AddTaxFields(fields, fiscalDocumentId, ref displayOrder, $"{paymentLabel} - ImpuestosP", pago, createdAtUtc);

            var relatedDocuments = pago.Elements().Where(static node => node.Name.LocalName == "DoctoRelacionado").ToList();
            for (var documentIndex = 0; documentIndex < relatedDocuments.Count; documentIndex++)
            {
                var relatedDocument = relatedDocuments[documentIndex];
                var relatedDocumentLabel = $"{paymentLabel} / Documento {documentIndex + 1}";
                AddField(fields, fiscalDocumentId, ref displayOrder, $"REP_DR_{paymentIndex + 1}_{documentIndex + 1}_UUID", $"{relatedDocumentLabel} - IdDocumento", GetAttribute(relatedDocument, "IdDocumento"), createdAtUtc);
                AddField(fields, fiscalDocumentId, ref displayOrder, $"REP_DR_{paymentIndex + 1}_{documentIndex + 1}_SERIE", $"{relatedDocumentLabel} - Serie", GetAttribute(relatedDocument, "Serie"), createdAtUtc);
                AddField(fields, fiscalDocumentId, ref displayOrder, $"REP_DR_{paymentIndex + 1}_{documentIndex + 1}_FOLIO", $"{relatedDocumentLabel} - Folio", GetAttribute(relatedDocument, "Folio"), createdAtUtc);
                AddField(fields, fiscalDocumentId, ref displayOrder, $"REP_DR_{paymentIndex + 1}_{documentIndex + 1}_MONEDA", $"{relatedDocumentLabel} - MonedaDR", GetAttribute(relatedDocument, "MonedaDR"), createdAtUtc);
                AddField(fields, fiscalDocumentId, ref displayOrder, $"REP_DR_{paymentIndex + 1}_{documentIndex + 1}_EQUIVALENCIA", $"{relatedDocumentLabel} - EquivalenciaDR", GetAttribute(relatedDocument, "EquivalenciaDR"), createdAtUtc);
                AddField(fields, fiscalDocumentId, ref displayOrder, $"REP_DR_{paymentIndex + 1}_{documentIndex + 1}_PARCIALIDAD", $"{relatedDocumentLabel} - NumParcialidad", GetAttribute(relatedDocument, "NumParcialidad"), createdAtUtc);
                AddField(fields, fiscalDocumentId, ref displayOrder, $"REP_DR_{paymentIndex + 1}_{documentIndex + 1}_SALDO_ANT", $"{relatedDocumentLabel} - ImpSaldoAnt", GetAttribute(relatedDocument, "ImpSaldoAnt"), createdAtUtc);
                AddField(fields, fiscalDocumentId, ref displayOrder, $"REP_DR_{paymentIndex + 1}_{documentIndex + 1}_IMP_PAGADO", $"{relatedDocumentLabel} - ImpPagado", GetAttribute(relatedDocument, "ImpPagado"), createdAtUtc);
                AddField(fields, fiscalDocumentId, ref displayOrder, $"REP_DR_{paymentIndex + 1}_{documentIndex + 1}_SALDO_INSOLUTO", $"{relatedDocumentLabel} - ImpSaldoInsoluto", GetAttribute(relatedDocument, "ImpSaldoInsoluto"), createdAtUtc);
                AddField(fields, fiscalDocumentId, ref displayOrder, $"REP_DR_{paymentIndex + 1}_{documentIndex + 1}_OBJETO_IMP", $"{relatedDocumentLabel} - ObjetoImpDR", GetAttribute(relatedDocument, "ObjetoImpDR"), createdAtUtc);

                AddTaxFields(fields, fiscalDocumentId, ref displayOrder, $"{relatedDocumentLabel} - ImpuestosDR", relatedDocument, createdAtUtc);
            }
        }

        return fields;
    }

    private static void AddTaxFields(
        List<FiscalDocumentSpecialFieldValue> fields,
        long fiscalDocumentId,
        ref int displayOrder,
        string labelPrefix,
        XElement scope,
        DateTime createdAtUtc)
    {
        foreach (var traslado in scope.Descendants().Where(static node => node.Name.LocalName is "TrasladoDR" or "TrasladoP"))
        {
            AddField(
                fields,
                fiscalDocumentId,
                ref displayOrder,
                $"REP_TAX_{displayOrder}",
                $"{labelPrefix} - {traslado.Name.LocalName}",
                SummarizeAttributes(traslado),
                createdAtUtc);
        }

        foreach (var retencion in scope.Descendants().Where(static node => node.Name.LocalName is "RetencionDR" or "RetencionP"))
        {
            AddField(
                fields,
                fiscalDocumentId,
                ref displayOrder,
                $"REP_TAX_{displayOrder}",
                $"{labelPrefix} - {retencion.Name.LocalName}",
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
