using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using Microsoft.Extensions.Options;
using Pineda.Facturacion.Application.Abstractions.Pac;
using Pineda.Facturacion.Application.Abstractions.Secrets;
using Pineda.Facturacion.Application.Contracts.Pac;
using Pineda.Facturacion.Infrastructure.FacturaloPlus.Options;

namespace Pineda.Facturacion.Infrastructure.FacturaloPlus.FacturaloPlus;

public class FacturaloPlusStampingGateway : IFiscalStampingGateway
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    private static readonly string[] CfdiTimeZoneIds =
    [
        "America/Mexico_City",
        "Central Standard Time (Mexico)"
    ];
    private static readonly TimeSpan FutureCfdiFechaTolerance = TimeSpan.FromMinutes(1);

    private readonly HttpClient _httpClient;
    private readonly FacturaloPlusOptions _options;
    private readonly ISecretReferenceResolver _secretReferenceResolver;

    public FacturaloPlusStampingGateway(
        HttpClient httpClient,
        IOptions<FacturaloPlusOptions> options,
        ISecretReferenceResolver secretReferenceResolver)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _secretReferenceResolver = secretReferenceResolver;
    }

    public async Task<FiscalStampingGatewayResult> StampAsync(
        FiscalStampingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var apiKey = await ResolveOptionalSecretAsync(_options.ApiKeyReference, cancellationToken);
        if (!string.IsNullOrWhiteSpace(_options.ApiKeyReference) && string.IsNullOrWhiteSpace(apiKey))
        {
            return ValidationFailed("Configured PAC API key reference could not be resolved.");
        }

        var certificateValue = await _secretReferenceResolver.ResolveAsync(request.CertificateReference, cancellationToken);
        if (string.IsNullOrWhiteSpace(certificateValue))
        {
            return ValidationFailed("Fiscal document certificate reference could not be resolved.");
        }

        var privateKeyValue = await _secretReferenceResolver.ResolveAsync(request.PrivateKeyReference, cancellationToken);
        if (string.IsNullOrWhiteSpace(privateKeyValue))
        {
            return ValidationFailed("Fiscal document private key reference could not be resolved.");
        }

        var redactedSummary = BuildRedactedRequestSummary(request);
        var providerRequestHash = ComputeSha256(JsonSerializer.Serialize(redactedSummary, JsonOptions));
        CertificateMetadata certificateMetadata;

        try
        {
            certificateMetadata = ExtractCertificateMetadata(certificateValue);
        }
        catch (CryptographicException)
        {
            return ValidationFailed("Fiscal document certificate PEM could not be parsed.");
        }

        if (!TryBuildComprobanteFecha(request.IssuedAtUtc, out var comprobanteFecha, out var fechaValidationError))
        {
            return ValidationFailed(fechaValidationError!);
        }

        var payload = BuildPayload(request, certificateMetadata, comprobanteFecha!);
        var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        var formPayload = BuildFormPayload(apiKey, payloadJson, privateKeyValue, certificateValue);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _options.StampPath)
        {
            Content = new FormUrlEncodedContent(formPayload)
        };

        HttpResponseMessage response;
        string responseContent;

        try
        {
            response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unavailable(providerRequestHash, "Provider timeout.");
        }
        catch (HttpRequestException)
        {
            return Unavailable(providerRequestHash, "Provider transport failure.");
        }

        var providerResponse = TryDeserialize(responseContent);
        var rawResponseSummaryJson = BuildRawResponseSummary(response.StatusCode, providerResponse, responseContent);

        if (response.IsSuccessStatusCode && IsProviderSuccess(providerResponse) && HasSuccessfulStampEvidence(providerResponse))
        {
            var successfulProviderResponse = providerResponse!;
            return new FiscalStampingGatewayResult
            {
                Outcome = FiscalStampingGatewayOutcome.Stamped,
                ProviderName = _options.ProviderName,
                ProviderOperation = "stamp",
                ProviderRequestHash = providerRequestHash,
                ProviderTrackingId = successfulProviderResponse.TrackingId,
                ProviderCode = successfulProviderResponse.Code,
                ProviderMessage = successfulProviderResponse.Message,
                Uuid = successfulProviderResponse.Uuid,
                StampedAtUtc = successfulProviderResponse.StampedAtUtc ?? DateTime.UtcNow,
                XmlContent = successfulProviderResponse.XmlContent,
                XmlHash = string.IsNullOrWhiteSpace(successfulProviderResponse.XmlContent) ? null : ComputeSha256(successfulProviderResponse.XmlContent),
                OriginalString = successfulProviderResponse.OriginalString,
                QrCodeTextOrUrl = successfulProviderResponse.QrCodeTextOrUrl,
                RawResponseSummaryJson = rawResponseSummaryJson
            };
        }

        if ((int)response.StatusCode >= 500)
        {
            return new FiscalStampingGatewayResult
            {
                Outcome = FiscalStampingGatewayOutcome.Unavailable,
                ProviderName = _options.ProviderName,
                ProviderOperation = "stamp",
                ProviderRequestHash = providerRequestHash,
                ProviderTrackingId = providerResponse?.TrackingId,
                ProviderCode = providerResponse?.Code ?? ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture),
                ProviderMessage = providerResponse?.Message,
                RawResponseSummaryJson = rawResponseSummaryJson,
                ErrorCode = providerResponse?.ErrorCode ?? "HTTP_" + (int)response.StatusCode,
                ErrorMessage = providerResponse?.ErrorMessage ?? "Provider unavailable."
            };
        }

        return new FiscalStampingGatewayResult
        {
            Outcome = FiscalStampingGatewayOutcome.Rejected,
            ProviderName = _options.ProviderName,
            ProviderOperation = "stamp",
            ProviderRequestHash = providerRequestHash,
            ProviderTrackingId = providerResponse?.TrackingId,
            ProviderCode = providerResponse?.Code ?? ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture),
            ProviderMessage = providerResponse?.Message,
            RawResponseSummaryJson = rawResponseSummaryJson,
            ErrorCode = providerResponse?.ErrorCode,
            ErrorMessage = providerResponse?.ErrorMessage ?? providerResponse?.Message ?? "Provider rejected the stamp request."
        };
    }

    private async Task<string?> ResolveOptionalSecretAsync(string? referenceKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(referenceKey))
        {
            return null;
        }

        return await _secretReferenceResolver.ResolveAsync(referenceKey, cancellationToken);
    }

    private static FacturaloPlusStampingPayload BuildPayload(
        FiscalStampingRequest request,
        CertificateMetadata certificateMetadata,
        string comprobanteFecha)
    {
        var currencyScale = ResolveCurrencyScale(request.CurrencyCode);
        var itemPayloads = request.Items
            .OrderBy(x => x.LineNumber)
            .Select(item => BuildConcepto(item, currencyScale))
            .ToList();
        var subTotal = RoundMonetary(itemPayloads.Sum(x => x.Importe), currencyScale);
        var descuento = RoundMonetary(itemPayloads.Sum(x => x.Descuento ?? 0m), currencyScale);

        var traslados = itemPayloads
            .Where(x => x.Impuestos?.Traslados is { Count: > 0 })
            .SelectMany(x => x.Impuestos!.Traslados!)
            .GroupBy(x => new { x.Impuesto, x.TipoFactor, x.TasaOCuota })
            .Select(group => new FacturaloPlusComprobanteTraslado
            {
                Base = RoundMonetary(group.Sum(x => x.Base), currencyScale),
                Impuesto = group.Key.Impuesto,
                TipoFactor = group.Key.TipoFactor,
                TasaOCuota = group.Key.TasaOCuota,
                Importe = RoundMonetary(group.Sum(x => x.Importe), currencyScale)
            })
            .ToList();
        var totalImpuestosTrasladados = RoundMonetary(traslados.Sum(x => x.Importe), currencyScale);
        var total = RoundMonetary(subTotal - descuento + totalImpuestosTrasladados, currencyScale);

        return new FacturaloPlusStampingPayload
        {
            Comprobante = new FacturaloPlusComprobante
            {
                Version = request.CfdiVersion,
                Serie = request.Series,
                Folio = request.Folio,
                Fecha = comprobanteFecha,
                Moneda = request.CurrencyCode,
                TipoDeComprobante = MapTipoDeComprobante(request.DocumentType),
                MetodoPago = request.PaymentMethodSat,
                FormaPago = request.PaymentFormSat,
                CondicionesDePago = request.PaymentCondition,
                Exportacion = "01",
                LugarExpedicion = request.IssuerPostalCode,
                TipoCambio = request.ExchangeRate == 1m ? null : request.ExchangeRate,
                SubTotal = subTotal,
                Descuento = descuento > 0 ? descuento : null,
                Total = total,
                NoCertificado = certificateMetadata.NoCertificado,
                Certificado = certificateMetadata.Certificado,
                Emisor = new FacturaloPlusComprobanteEmisor
                {
                    Rfc = request.IssuerRfc,
                    Nombre = request.IssuerLegalName,
                    RegimenFiscal = request.IssuerFiscalRegimeCode
                },
                Receptor = new FacturaloPlusComprobanteReceptor
                {
                    Rfc = request.ReceiverRfc,
                    Nombre = request.ReceiverLegalName,
                    DomicilioFiscalReceptor = request.ReceiverPostalCode,
                    RegimenFiscalReceptor = request.ReceiverFiscalRegimeCode,
                    UsoCFDI = request.ReceiverCfdiUseCode,
                    ResidenciaFiscal = request.ReceiverCountryCode is { Length: > 0 } countryCode && !string.Equals(countryCode, "MX", StringComparison.OrdinalIgnoreCase)
                        ? request.ReceiverCountryCode
                        : null,
                    NumRegIdTrib = request.ReceiverForeignTaxRegistration
                },
                Conceptos = itemPayloads,
                Impuestos = traslados.Count == 0
                    ? null
                    : new FacturaloPlusComprobanteImpuestos
                    {
                        TotalImpuestosTrasladados = totalImpuestosTrasladados,
                        Traslados = traslados
                    }
            }
        };
    }

    private static bool TryBuildComprobanteFecha(
        DateTime issuedAtUtc,
        out string? comprobanteFecha,
        out string? validationError)
    {
        comprobanteFecha = null;
        validationError = null;

        var cfdiTimeZone = ResolveCfdiTimeZone();
        if (cfdiTimeZone is null)
        {
            validationError = "CFDI emission time zone 'America/Mexico_City' could not be resolved.";
            return false;
        }

        var normalizedIssuedAtUtc = NormalizeUtc(issuedAtUtc);
        var localIssuedAt = TrimToSeconds(TimeZoneInfo.ConvertTimeFromUtc(normalizedIssuedAtUtc, cfdiTimeZone));
        var localNow = TrimToSeconds(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, cfdiTimeZone));

        if (localIssuedAt > localNow.Add(FutureCfdiFechaTolerance))
        {
            validationError = $"Fiscal document issued-at UTC resolves to a future local CFDI emission date for '{cfdiTimeZone.Id}'.";
            return false;
        }

        comprobanteFecha = localIssuedAt.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
        return true;
    }

    private static FacturaloPlusComprobanteConcepto BuildConcepto(FiscalStampingRequestItem item, int currencyScale)
    {
        var valorUnitario = RoundMonetary(item.UnitPrice, currencyScale);
        var importe = RoundMonetary(valorUnitario * item.Quantity, currencyScale);
        var descuento = item.DiscountAmount > 0
            ? RoundMonetary(item.DiscountAmount, currencyScale)
            : (decimal?)null;
        var importeNeto = RoundMonetary(importe - (descuento ?? 0m), currencyScale);
        var traslados = BuildConceptoTraslados(item, importeNeto, currencyScale);

        return new FacturaloPlusComprobanteConcepto
        {
            ClaveProdServ = item.SatProductServiceCode,
            NoIdentificacion = item.InternalCode,
            Cantidad = item.Quantity,
            ClaveUnidad = item.SatUnitCode,
            Unidad = item.UnitText,
            Descripcion = item.Description,
            ValorUnitario = valorUnitario,
            Importe = importe,
            Descuento = descuento,
            ObjetoImp = item.TaxObjectCode,
            Impuestos = traslados.Count == 0
                ? null
                : new FacturaloPlusComprobanteConceptoImpuestos
                {
                    Traslados = traslados
                }
        };
    }

    private static List<FacturaloPlusComprobanteTraslado> BuildConceptoTraslados(FiscalStampingRequestItem item, decimal importeNeto, int currencyScale)
    {
        if (!string.Equals(item.TaxObjectCode, "02", StringComparison.Ordinal))
        {
            return [];
        }

        var trasladoImporte = RoundMonetary(importeNeto * item.VatRate, currencyScale);

        return
        [
            new FacturaloPlusComprobanteTraslado
            {
                Base = importeNeto,
                Impuesto = "002",
                TipoFactor = "Tasa",
                TasaOCuota = FormatTasaOCuota(item.VatRate),
                Importe = trasladoImporte
            }
        ];
    }

    private static int ResolveCurrencyScale(string currencyCode)
    {
        return string.Equals(currencyCode, "MXN", StringComparison.OrdinalIgnoreCase) ? 2 : 2;
    }

    private static decimal RoundMonetary(decimal value, int currencyScale)
    {
        return Math.Round(value, currencyScale, MidpointRounding.AwayFromZero);
    }

    private static string FormatTasaOCuota(decimal tasaOcuota)
    {
        return tasaOcuota.ToString("0.000000", CultureInfo.InvariantCulture);
    }

    private static TimeZoneInfo? ResolveCfdiTimeZone()
    {
        foreach (var timeZoneId in CfdiTimeZoneIds)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return null;
    }

    private static DateTime NormalizeUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static DateTime TrimToSeconds(DateTime value)
    {
        return new DateTime(
            value.Year,
            value.Month,
            value.Day,
            value.Hour,
            value.Minute,
            value.Second,
            value.Kind);
    }

    private static CertificateMetadata ExtractCertificateMetadata(string certificatePem)
    {
        var certificate = X509Certificate2.CreateFromPem(certificatePem);
        var certificateBytes = certificate.Export(X509ContentType.Cert);
        var serialBytes = certificate.GetSerialNumber().Reverse().ToArray();

        var noCertificado = serialBytes.All(static x => x is >= (byte)'0' and <= (byte)'9')
            ? Encoding.ASCII.GetString(serialBytes)
            : Convert.ToHexString(serialBytes);

        return new CertificateMetadata(
            noCertificado,
            Convert.ToBase64String(certificateBytes));
    }

    private static string MapTipoDeComprobante(string documentType)
    {
        if (string.Equals(documentType, "INVOICE", StringComparison.OrdinalIgnoreCase))
        {
            return "I";
        }

        return documentType;
    }

    private static IReadOnlyDictionary<string, string> BuildFormPayload(
        string? apiKey,
        string payloadJson,
        string privateKeyPem,
        string certificatePem)
    {
        return new Dictionary<string, string>
        {
            ["apikey"] = apiKey ?? string.Empty,
            ["jsonB64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(payloadJson)),
            ["keyPEM"] = privateKeyPem,
            ["cerPEM"] = certificatePem
        };
    }

    private static object BuildRedactedRequestSummary(FiscalStampingRequest request)
    {
        return new
        {
            request.FiscalDocumentId,
            request.PacEnvironment,
            request.CfdiVersion,
            request.DocumentType,
            request.Series,
            request.Folio,
            request.IssuedAtUtc,
            request.CurrencyCode,
            request.ExchangeRate,
            request.PaymentMethodSat,
            request.PaymentFormSat,
            request.PaymentCondition,
            request.IsCreditSale,
            request.CreditDays,
            request.IssuerRfc,
            request.IssuerLegalName,
            request.IssuerFiscalRegimeCode,
            request.IssuerPostalCode,
            request.ReceiverRfc,
            request.ReceiverLegalName,
            request.ReceiverFiscalRegimeCode,
            request.ReceiverCfdiUseCode,
            request.ReceiverPostalCode,
            request.ReceiverCountryCode,
            request.ReceiverForeignTaxRegistration,
            request.Subtotal,
            request.DiscountTotal,
            request.TaxTotal,
            request.Total,
            HasCertificateReference = !string.IsNullOrWhiteSpace(request.CertificateReference),
            HasPrivateKeyReference = !string.IsNullOrWhiteSpace(request.PrivateKeyReference),
            HasPrivateKeyPasswordReference = !string.IsNullOrWhiteSpace(request.PrivateKeyPasswordReference),
            Items = request.Items
                .OrderBy(x => x.LineNumber)
                .Select(x => new
                {
                    x.LineNumber,
                    x.InternalCode,
                    x.Description,
                    x.Quantity,
                    x.UnitPrice,
                    x.DiscountAmount,
                    x.Subtotal,
                    x.TaxTotal,
                    x.Total,
                    x.SatProductServiceCode,
                    x.SatUnitCode,
                    x.TaxObjectCode,
                    x.VatRate,
                    x.UnitText
                })
                .ToList()
        };
    }

    private static string BuildRawResponseSummary(HttpStatusCode statusCode, FacturaloPlusStampingResponse? response, string rawContent)
    {
        var summary = new
        {
            HttpStatusCode = (int)statusCode,
            response?.Success,
            response?.TrackingId,
            response?.Code,
            response?.Message,
            response?.Uuid,
            response?.StampedAtUtc,
            response?.HasSuccessfulStampEvidence,
            response?.ErrorCode,
            response?.ErrorMessage,
            RawContentPreview = string.IsNullOrWhiteSpace(rawContent)
                ? null
                : rawContent.Length <= 500 ? rawContent : rawContent[..500]
        };

        return JsonSerializer.Serialize(summary, JsonOptions);
    }

    private static FiscalStampingGatewayResult ValidationFailed(string errorMessage)
    {
        return new FiscalStampingGatewayResult
        {
            Outcome = FiscalStampingGatewayOutcome.ValidationFailed,
            ProviderName = "FacturaloPlus",
            ProviderOperation = "stamp",
            ErrorMessage = errorMessage,
            RawResponseSummaryJson = JsonSerializer.Serialize(new { error = errorMessage }, JsonOptions)
        };
    }

    private static FiscalStampingGatewayResult Unavailable(string providerRequestHash, string errorMessage)
    {
        return new FiscalStampingGatewayResult
        {
            Outcome = FiscalStampingGatewayOutcome.Unavailable,
            ProviderName = "FacturaloPlus",
            ProviderOperation = "stamp",
            ProviderRequestHash = providerRequestHash,
            ErrorMessage = errorMessage,
            RawResponseSummaryJson = JsonSerializer.Serialize(new { error = errorMessage }, JsonOptions)
        };
    }

    private static bool IsProviderSuccess(FacturaloPlusStampingResponse? response)
    {
        return response?.Success == true
            || string.Equals(response?.Code, "200", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasSuccessfulStampEvidence(FacturaloPlusStampingResponse? response)
    {
        return response is not null
            && (response.HasSuccessfulStampEvidence
                || !string.IsNullOrWhiteSpace(response.Uuid));
    }

    private static FacturaloPlusStampingResponse? TryDeserialize(string rawContent)
    {
        if (string.IsNullOrWhiteSpace(rawContent))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(rawContent);
            var root = document.RootElement;
            var response = new FacturaloPlusStampingResponse
            {
                Success = ReadBoolean(root, "success") ?? false,
                TrackingId = ReadString(root, "trackingId", "trackingID", "TrackingId"),
                Code = ReadString(root, "code", "Code"),
                Message = ReadString(root, "message", "Message"),
                Uuid = ReadString(root, "uuid", "UUID", "Uuid"),
                StampedAtUtc = ReadUtcDateTime(root, "stampedAtUtc", "StampedAtUtc", "fechaTimbrado", "FechaTimbrado"),
                XmlContent = ReadString(root, "xmlContent", "xml", "XML", "XmlContent"),
                OriginalString = ReadString(root, "originalString", "OriginalString", "cadenaOriginal", "CadenaOriginal", "cadenaOriginalTFD", "CadenaOriginalTFD"),
                QrCodeTextOrUrl = ReadString(root, "qrCodeTextOrUrl", "QrCodeTextOrUrl", "qrCode", "QRCode", "qr", "QR"),
                ErrorCode = ReadString(root, "errorCode", "ErrorCode"),
                ErrorMessage = ReadString(root, "errorMessage", "ErrorMessage")
            };

            if (TryGetProperty(root, out var dataElement, "data", "Data"))
            {
                PopulateFromProviderData(dataElement, response);
            }

            PopulateStampEvidenceFromXml(response);
            response.Success = response.Success || string.Equals(response.Code, "200", StringComparison.OrdinalIgnoreCase);

            return response;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string ComputeSha256(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static void PopulateFromProviderData(JsonElement dataElement, FacturaloPlusStampingResponse response)
    {
        if (dataElement.ValueKind == JsonValueKind.String)
        {
            var rawValue = dataElement.GetString();
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return;
            }

            var trimmed = rawValue.Trim();
            if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
            {
                try
                {
                    using var nestedDocument = JsonDocument.Parse(trimmed);
                    PopulateFromProviderData(nestedDocument.RootElement, response);
                    return;
                }
                catch (JsonException)
                {
                }
            }

            if (trimmed.StartsWith('<'))
            {
                response.XmlContent ??= rawValue;
            }

            return;
        }

        if (dataElement.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        response.Uuid ??= ReadString(dataElement, "uuid", "UUID", "Uuid");
        response.StampedAtUtc ??= ReadUtcDateTime(dataElement, "stampedAtUtc", "StampedAtUtc", "fechaTimbrado", "FechaTimbrado");
        response.XmlContent ??= ReadString(dataElement, "xmlContent", "xml", "XML", "XmlContent");
        response.OriginalString ??= ReadString(dataElement, "originalString", "OriginalString", "cadenaOriginal", "CadenaOriginal", "cadenaOriginalTFD", "CadenaOriginalTFD");
        response.QrCodeTextOrUrl ??= ReadString(dataElement, "qrCodeTextOrUrl", "QrCodeTextOrUrl", "qrCode", "QRCode", "qr", "QR");
    }

    private static void PopulateStampEvidenceFromXml(FacturaloPlusStampingResponse response)
    {
        if (string.IsNullOrWhiteSpace(response.XmlContent))
        {
            return;
        }

        try
        {
            var document = XDocument.Parse(response.XmlContent, LoadOptions.PreserveWhitespace);
            var timbre = document
                .Descendants()
                .FirstOrDefault(static element => string.Equals(element.Name.LocalName, "TimbreFiscalDigital", StringComparison.Ordinal));

            if (timbre is null)
            {
                return;
            }

            response.Uuid ??= GetAttributeValue(timbre, "UUID");
            response.StampedAtUtc ??= TryParseCfdiLocalDateTimeAsUtc(GetAttributeValue(timbre, "FechaTimbrado"));
            response.HasSuccessfulStampEvidence = !string.IsNullOrWhiteSpace(response.Uuid) || response.StampedAtUtc is not null;
        }
        catch (Exception)
        {
        }
    }

    private static string? GetAttributeValue(XElement element, string localName)
    {
        return element.Attributes().FirstOrDefault(attribute => string.Equals(attribute.Name.LocalName, localName, StringComparison.Ordinal))?.Value;
    }

    private static DateTime? TryParseCfdiLocalDateTimeAsUtc(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (ContainsExplicitOffset(value) && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var offsetValue))
        {
            return offsetValue.UtcDateTime;
        }

        if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var localDateTime))
        {
            return null;
        }

        var cfdiTimeZone = ResolveCfdiTimeZone();
        if (cfdiTimeZone is null)
        {
            return null;
        }

        var unspecifiedLocalDateTime = DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecifiedLocalDateTime, cfdiTimeZone);
    }

    private static bool ContainsExplicitOffset(string value)
    {
        return value.EndsWith("Z", StringComparison.OrdinalIgnoreCase)
            || value.Contains('+', StringComparison.Ordinal)
            || value.LastIndexOf('-', value.Length - 1) > value.IndexOf('T');
    }

    private static string? ReadString(JsonElement element, params string[] propertyNames)
    {
        if (!TryGetProperty(element, out var property, propertyNames))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.GetRawText(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            _ => null
        };
    }

    private static bool? ReadBoolean(JsonElement element, params string[] propertyNames)
    {
        if (!TryGetProperty(element, out var property, propertyNames))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(property.GetString(), out var parsedValue) => parsedValue,
            _ => null
        };
    }

    private static DateTime? ReadUtcDateTime(JsonElement element, params string[] propertyNames)
    {
        var value = ReadString(element, propertyNames);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var offsetValue))
        {
            return offsetValue.UtcDateTime;
        }

        return TryParseCfdiLocalDateTimeAsUtc(value);
    }

    private static bool TryGetProperty(JsonElement element, out JsonElement property, params string[] propertyNames)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var candidate in element.EnumerateObject())
            {
                foreach (var propertyName in propertyNames)
                {
                    if (string.Equals(candidate.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                    {
                        property = candidate.Value;
                        return true;
                    }
                }
            }
        }

        property = default;
        return false;
    }

    private sealed class FacturaloPlusStampingPayload
    {
        [JsonPropertyName("Comprobante")]
        public FacturaloPlusComprobante Comprobante { get; init; } = new();
    }

    private sealed class FacturaloPlusComprobante
    {
        [JsonPropertyName("Version")]
        public string Version { get; init; } = string.Empty;
        [JsonPropertyName("Serie")]
        public string? Serie { get; init; }
        [JsonPropertyName("Folio")]
        public string? Folio { get; init; }
        [JsonPropertyName("Fecha")]
        public string Fecha { get; init; } = string.Empty;
        [JsonPropertyName("Moneda")]
        public string Moneda { get; init; } = string.Empty;
        [JsonPropertyName("TipoCambio")]
        public decimal? TipoCambio { get; init; }
        [JsonPropertyName("TipoDeComprobante")]
        public string TipoDeComprobante { get; init; } = string.Empty;
        [JsonPropertyName("MetodoPago")]
        public string MetodoPago { get; init; } = string.Empty;
        [JsonPropertyName("FormaPago")]
        public string FormaPago { get; init; } = string.Empty;
        [JsonPropertyName("CondicionesDePago")]
        public string? CondicionesDePago { get; init; }
        [JsonPropertyName("Exportacion")]
        public string Exportacion { get; init; } = string.Empty;
        [JsonPropertyName("LugarExpedicion")]
        public string LugarExpedicion { get; init; } = string.Empty;
        [JsonPropertyName("SubTotal")]
        public decimal SubTotal { get; init; }
        [JsonPropertyName("Descuento")]
        public decimal? Descuento { get; init; }
        [JsonPropertyName("Total")]
        public decimal Total { get; init; }
        [JsonPropertyName("NoCertificado")]
        public string NoCertificado { get; init; } = string.Empty;
        [JsonPropertyName("Certificado")]
        public string Certificado { get; init; } = string.Empty;
        [JsonPropertyName("Emisor")]
        public FacturaloPlusComprobanteEmisor Emisor { get; init; } = new();
        [JsonPropertyName("Receptor")]
        public FacturaloPlusComprobanteReceptor Receptor { get; init; } = new();
        [JsonPropertyName("Conceptos")]
        public List<FacturaloPlusComprobanteConcepto> Conceptos { get; init; } = [];
        [JsonPropertyName("Impuestos")]
        public FacturaloPlusComprobanteImpuestos? Impuestos { get; init; }
    }

    private sealed class FacturaloPlusComprobanteEmisor
    {
        [JsonPropertyName("Rfc")]
        public string Rfc { get; init; } = string.Empty;
        [JsonPropertyName("Nombre")]
        public string Nombre { get; init; } = string.Empty;
        [JsonPropertyName("RegimenFiscal")]
        public string RegimenFiscal { get; init; } = string.Empty;
    }

    private sealed class FacturaloPlusComprobanteReceptor
    {
        [JsonPropertyName("Rfc")]
        public string Rfc { get; init; } = string.Empty;
        [JsonPropertyName("Nombre")]
        public string Nombre { get; init; } = string.Empty;
        [JsonPropertyName("DomicilioFiscalReceptor")]
        public string DomicilioFiscalReceptor { get; init; } = string.Empty;
        [JsonPropertyName("RegimenFiscalReceptor")]
        public string RegimenFiscalReceptor { get; init; } = string.Empty;
        [JsonPropertyName("UsoCFDI")]
        public string UsoCFDI { get; init; } = string.Empty;
        [JsonPropertyName("ResidenciaFiscal")]
        public string? ResidenciaFiscal { get; init; }
        [JsonPropertyName("NumRegIdTrib")]
        public string? NumRegIdTrib { get; init; }
    }

    private sealed class FacturaloPlusComprobanteConcepto
    {
        [JsonPropertyName("ClaveProdServ")]
        public string ClaveProdServ { get; init; } = string.Empty;
        [JsonPropertyName("NoIdentificacion")]
        public string NoIdentificacion { get; init; } = string.Empty;
        [JsonPropertyName("Cantidad")]
        public decimal Cantidad { get; init; }
        [JsonPropertyName("ClaveUnidad")]
        public string ClaveUnidad { get; init; } = string.Empty;
        [JsonPropertyName("Unidad")]
        public string? Unidad { get; init; }
        [JsonPropertyName("Descripcion")]
        public string Descripcion { get; init; } = string.Empty;
        [JsonPropertyName("ValorUnitario")]
        public decimal ValorUnitario { get; init; }
        [JsonPropertyName("Importe")]
        public decimal Importe { get; init; }
        [JsonPropertyName("Descuento")]
        public decimal? Descuento { get; init; }
        [JsonPropertyName("ObjetoImp")]
        public string ObjetoImp { get; init; } = string.Empty;
        [JsonPropertyName("Impuestos")]
        public FacturaloPlusComprobanteConceptoImpuestos? Impuestos { get; init; }
    }

    private sealed class FacturaloPlusComprobanteConceptoImpuestos
    {
        [JsonPropertyName("Traslados")]
        public List<FacturaloPlusComprobanteTraslado>? Traslados { get; init; }
    }

    private sealed class FacturaloPlusComprobanteImpuestos
    {
        [JsonPropertyName("TotalImpuestosTrasladados")]
        public decimal TotalImpuestosTrasladados { get; init; }
        [JsonPropertyName("Traslados")]
        public List<FacturaloPlusComprobanteTraslado> Traslados { get; init; } = [];
    }

    private sealed class FacturaloPlusComprobanteTraslado
    {
        [JsonPropertyName("Base")]
        public decimal Base { get; init; }
        [JsonPropertyName("Impuesto")]
        public string Impuesto { get; init; } = string.Empty;
        [JsonPropertyName("TipoFactor")]
        public string TipoFactor { get; init; } = string.Empty;
        [JsonPropertyName("TasaOCuota")]
        public string TasaOCuota { get; init; } = string.Empty;
        [JsonPropertyName("Importe")]
        public decimal Importe { get; init; }
    }

    private sealed class FacturaloPlusStampingResponse
    {
        public bool Success { get; set; }
        public string? TrackingId { get; set; }
        public string? Code { get; set; }
        public string? Message { get; set; }
        public string? Uuid { get; set; }
        public DateTime? StampedAtUtc { get; set; }
        public string? XmlContent { get; set; }
        public string? OriginalString { get; set; }
        public string? QrCodeTextOrUrl { get; set; }
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public bool HasSuccessfulStampEvidence { get; set; }
    }

    private sealed record CertificateMetadata(
        string NoCertificado,
        string Certificado);
}
