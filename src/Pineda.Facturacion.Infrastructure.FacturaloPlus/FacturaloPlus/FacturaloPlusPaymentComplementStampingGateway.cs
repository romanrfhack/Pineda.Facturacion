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

public class FacturaloPlusPaymentComplementStampingGateway : IPaymentComplementStampingGateway
{
    private static readonly string[] CfdiTimeZoneIds =
    [
        "America/Mexico_City",
        "Central Standard Time (Mexico)"
    ];
    private static readonly TimeSpan FutureCfdiFechaTolerance = TimeSpan.FromMinutes(1);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly FacturaloPlusOptions _options;
    private readonly ISecretReferenceResolver _secretReferenceResolver;

    public FacturaloPlusPaymentComplementStampingGateway(
        HttpClient httpClient,
        IOptions<FacturaloPlusOptions> options,
        ISecretReferenceResolver secretReferenceResolver)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _secretReferenceResolver = secretReferenceResolver;
    }

    public async Task<PaymentComplementStampingGatewayResult> StampAsync(
        PaymentComplementStampingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var providerOperation = NormalizeRelativePath(_options.PaymentComplementStampPath);
        if (string.IsNullOrWhiteSpace(providerOperation))
        {
            return ValidationFailed("Configured payment complement stamp path is empty.");
        }

        var apiKey = await ResolveOptionalSecretAsync(_options.ApiKeyReference, cancellationToken);
        if (!string.IsNullOrWhiteSpace(_options.ApiKeyReference) && string.IsNullOrWhiteSpace(apiKey))
        {
            return ValidationFailed("Configured PAC API key reference could not be resolved.");
        }

        var certificateValue = await _secretReferenceResolver.ResolveAsync(request.CertificateReference, cancellationToken);
        if (string.IsNullOrWhiteSpace(certificateValue))
        {
            return ValidationFailed("Payment complement certificate reference could not be resolved.");
        }

        var privateKeyValue = await _secretReferenceResolver.ResolveAsync(request.PrivateKeyReference, cancellationToken);
        if (string.IsNullOrWhiteSpace(privateKeyValue))
        {
            return ValidationFailed("Payment complement private key reference could not be resolved.");
        }

        CertificateMetadata certificateMetadata;
        try
        {
            certificateMetadata = ExtractCertificateMetadata(certificateValue);
        }
        catch (CryptographicException)
        {
            return ValidationFailed("Payment complement certificate PEM could not be parsed.");
        }

        if (!TryBuildCfdiLocalDateTime(request.IssuedAtUtc, out var comprobanteFecha, out var issuedAtValidationError))
        {
            return ValidationFailed(issuedAtValidationError!);
        }

        if (!TryBuildCfdiLocalDateTime(request.PaymentDateUtc, out var paymentFecha, out var paymentDateValidationError))
        {
            return ValidationFailed(paymentDateValidationError!);
        }

        var redactedSummary = BuildRedactedRequestSummary(request);
        var providerRequestHash = ComputeSha256(JsonSerializer.Serialize(redactedSummary, JsonOptions));
        var payload = BuildPayload(request, certificateMetadata, comprobanteFecha!, paymentFecha!);
        var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        var formPayload = BuildFormPayload(apiKey, payloadJson, privateKeyValue, certificateValue);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, providerOperation)
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
        var requestUri = response.RequestMessage?.RequestUri?.ToString() ?? httpRequest.RequestUri?.ToString();
        var rawResponseSummaryJson = BuildRawResponseSummary(response.StatusCode, providerOperation, requestUri, providerResponse, responseContent);

        if (response.IsSuccessStatusCode && IsProviderSuccess(providerResponse) && HasSuccessfulStampEvidence(providerResponse))
        {
            var successfulProviderResponse = providerResponse!;
            return new PaymentComplementStampingGatewayResult
            {
                Outcome = PaymentComplementStampingGatewayOutcome.Stamped,
                ProviderName = _options.ProviderName,
                ProviderOperation = providerOperation,
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
            return new PaymentComplementStampingGatewayResult
            {
                Outcome = PaymentComplementStampingGatewayOutcome.Unavailable,
                ProviderName = _options.ProviderName,
                ProviderOperation = providerOperation,
                ProviderRequestHash = providerRequestHash,
                ProviderTrackingId = providerResponse?.TrackingId,
                ProviderCode = providerResponse?.Code ?? ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture),
                ProviderMessage = providerResponse?.Message,
                RawResponseSummaryJson = rawResponseSummaryJson,
                ErrorCode = providerResponse?.ErrorCode ?? "HTTP_" + (int)response.StatusCode,
                ErrorMessage = providerResponse?.ErrorMessage ?? "Provider unavailable."
            };
        }

        return new PaymentComplementStampingGatewayResult
        {
            Outcome = PaymentComplementStampingGatewayOutcome.Rejected,
            ProviderName = _options.ProviderName,
            ProviderOperation = providerOperation,
            ProviderRequestHash = providerRequestHash,
            ProviderTrackingId = providerResponse?.TrackingId,
            ProviderCode = providerResponse?.Code ?? ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture),
            ProviderMessage = providerResponse?.Message,
            RawResponseSummaryJson = rawResponseSummaryJson,
            ErrorCode = providerResponse?.ErrorCode,
            ErrorMessage = providerResponse?.ErrorMessage ?? providerResponse?.Message ?? "Provider rejected the payment complement stamp request."
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

    private static FacturaloPlusPaymentComplementStampingPayload BuildPayload(
        PaymentComplementStampingRequest request,
        CertificateMetadata certificateMetadata,
        string comprobanteFecha,
        string paymentFecha)
    {
        var relatedDocuments = request.RelatedDocuments
            .OrderBy(x => x.AccountsReceivableInvoiceId)
            .ThenBy(x => x.InstallmentNumber)
            .Select(BuildRelatedDocument)
            .ToList();
        var paymentTransfers = relatedDocuments
            .Where(x => x.ImpuestosDR?.TrasladosDR is { Count: > 0 })
            .SelectMany(x => x.ImpuestosDR!.TrasladosDR!)
            .GroupBy(x => new { x.ImpuestoDR, x.TipoFactorDR, x.TasaOCuotaDR })
            .Select(group => new FacturaloPlusPagoTrasladoP
            {
                BaseP = NormalizeMoney(group.Sum(x => x.BaseDR)),
                ImpuestoP = group.Key.ImpuestoDR,
                TipoFactorP = group.Key.TipoFactorDR,
                TasaOCuotaP = group.Key.TasaOCuotaDR,
                ImporteP = NormalizeMoney(group.Sum(x => x.ImporteDR))
            })
            .ToList();

        return new FacturaloPlusPaymentComplementStampingPayload
        {
            Comprobante = new FacturaloPlusPaymentComplementComprobante
            {
                Version = request.CfdiVersion,
                Fecha = comprobanteFecha,
                Moneda = "XXX",
                TipoDeComprobante = "P",
                Exportacion = "01",
                LugarExpedicion = request.IssuerPostalCode,
                SubTotal = 0m,
                Total = 0m,
                NoCertificado = certificateMetadata.NoCertificado,
                Certificado = certificateMetadata.Certificado,
                Emisor = new FacturaloPlusPaymentComplementComprobanteEmisor
                {
                    Rfc = request.IssuerRfc,
                    Nombre = request.IssuerLegalName,
                    RegimenFiscal = request.IssuerFiscalRegimeCode
                },
                Receptor = new FacturaloPlusPaymentComplementComprobanteReceptor
                {
                    Rfc = request.ReceiverRfc,
                    Nombre = request.ReceiverLegalName,
                    DomicilioFiscalReceptor = request.ReceiverPostalCode,
                    RegimenFiscalReceptor = request.ReceiverFiscalRegimeCode,
                    UsoCFDI = "CP01",
                    ResidenciaFiscal = request.ReceiverCountryCode is { Length: > 0 } countryCode && !string.Equals(countryCode, "MX", StringComparison.OrdinalIgnoreCase)
                        ? request.ReceiverCountryCode
                        : null,
                    NumRegIdTrib = request.ReceiverForeignTaxRegistration
                },
                Conceptos =
                [
                    new FacturaloPlusPaymentComplementConcepto
                    {
                        ClaveProdServ = "84111506",
                        Cantidad = 1m,
                        ClaveUnidad = "ACT",
                        Descripcion = "Pago",
                        ValorUnitario = 0m,
                        Importe = 0m,
                        ObjetoImp = "01"
                    }
                ],
                Complemento = new FacturaloPlusPaymentComplementComplemento
                {
                    Pagos20 = new FacturaloPlusPaymentComplementPagos20
                    {
                        Version = "2.0",
                        Totales = BuildTotales(request.TotalPaymentsAmount, paymentTransfers),
                        Pago =
                        [
                            new FacturaloPlusPago
                            {
                                FechaPago = paymentFecha,
                                FormaDePagoP = request.PaymentFormSat,
                                MonedaP = request.CurrencyCode,
                                Monto = NormalizeMoney(request.TotalPaymentsAmount),
                                DoctoRelacionado = relatedDocuments,
                                ImpuestosP = paymentTransfers.Count == 0
                                    ? null
                                    : new FacturaloPlusPagoImpuestosP
                                    {
                                        TrasladosP = paymentTransfers
                                    }
                            }
                        ]
                    }
                }
            }
        };
    }

    private static FacturaloPlusPagoDoctoRelacionado BuildRelatedDocument(PaymentComplementStampingRequestRelatedDocument relatedDocument)
    {
        var transfers = relatedDocument.TaxTransfers
            .Select(x => new FacturaloPlusPagoTrasladoDR
            {
                BaseDR = NormalizeMoney(x.BaseAmount),
                ImpuestoDR = x.TaxCode,
                TipoFactorDR = x.FactorType,
                TasaOCuotaDR = FormatRate(x.Rate),
                ImporteDR = NormalizeMoney(x.TaxAmount)
            })
            .ToList();

        return new FacturaloPlusPagoDoctoRelacionado
        {
            IdDocumento = relatedDocument.RelatedDocumentUuid,
            MonedaDR = relatedDocument.CurrencyCode,
            NumParcialidad = relatedDocument.InstallmentNumber,
            ImpSaldoAnt = NormalizeMoney(relatedDocument.PreviousBalance),
            ImpPagado = NormalizeMoney(relatedDocument.PaidAmount),
            ImpSaldoInsoluto = NormalizeMoney(relatedDocument.RemainingBalance),
            ObjetoImpDR = string.IsNullOrWhiteSpace(relatedDocument.TaxObjectCode) ? "01" : relatedDocument.TaxObjectCode,
            MetodoDePagoDR = "PPD",
            ImpuestosDR = transfers.Count == 0
                ? null
                : new FacturaloPlusPagoImpuestosDR
                {
                    TrasladosDR = transfers
                }
        };
    }

    private static FacturaloPlusPagoTotales BuildTotales(
        decimal totalPaymentsAmount,
        IReadOnlyCollection<FacturaloPlusPagoTrasladoP> paymentTransfers)
    {
        var totals = new FacturaloPlusPagoTotales
        {
            MontoTotalPagos = NormalizeMoney(totalPaymentsAmount)
        };

        foreach (var transfer in paymentTransfers)
        {
            if (!string.Equals(transfer.ImpuestoP, "002", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(transfer.TipoFactorP, "Tasa", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(transfer.TasaOCuotaP, "0.160000", StringComparison.Ordinal))
            {
                totals.TotalTrasladosBaseIVA16 = NormalizeMoney((totals.TotalTrasladosBaseIVA16 ?? 0m) + transfer.BaseP);
                totals.TotalTrasladosImpuestoIVA16 = NormalizeMoney((totals.TotalTrasladosImpuestoIVA16 ?? 0m) + transfer.ImporteP);
            }
            else if (string.Equals(transfer.TasaOCuotaP, "0.080000", StringComparison.Ordinal))
            {
                totals.TotalTrasladosBaseIVA8 = NormalizeMoney((totals.TotalTrasladosBaseIVA8 ?? 0m) + transfer.BaseP);
                totals.TotalTrasladosImpuestoIVA8 = NormalizeMoney((totals.TotalTrasladosImpuestoIVA8 ?? 0m) + transfer.ImporteP);
            }
            else if (string.Equals(transfer.TasaOCuotaP, "0.000000", StringComparison.Ordinal))
            {
                totals.TotalTrasladosBaseIVA0 = NormalizeMoney((totals.TotalTrasladosBaseIVA0 ?? 0m) + transfer.BaseP);
                totals.TotalTrasladosImpuestoIVA0 = NormalizeMoney((totals.TotalTrasladosImpuestoIVA0 ?? 0m) + transfer.ImporteP);
            }
        }

        return totals;
    }

    private static object BuildRedactedRequestSummary(PaymentComplementStampingRequest request)
    {
        return new
        {
            request.PaymentComplementDocumentId,
            request.PacEnvironment,
            request.CfdiVersion,
            request.DocumentType,
            request.IssuedAtUtc,
            request.PaymentDateUtc,
            request.PaymentFormSat,
            request.CurrencyCode,
            request.TotalPaymentsAmount,
            request.IssuerRfc,
            request.IssuerLegalName,
            request.IssuerFiscalRegimeCode,
            request.IssuerPostalCode,
            request.ReceiverRfc,
            request.ReceiverLegalName,
            request.ReceiverFiscalRegimeCode,
            request.ReceiverPostalCode,
            request.ReceiverCountryCode,
            request.ReceiverForeignTaxRegistration,
            RelatedDocuments = request.RelatedDocuments.Select(x => new
            {
                x.AccountsReceivableInvoiceId,
                x.FiscalDocumentId,
                x.RelatedDocumentUuid,
                x.InstallmentNumber,
                x.PreviousBalance,
                x.PaidAmount,
                x.RemainingBalance,
                x.CurrencyCode,
                x.TaxObjectCode,
                TaxTransfers = x.TaxTransfers.Select(y => new
                {
                    y.TaxCode,
                    y.FactorType,
                    y.Rate,
                    y.BaseAmount,
                    y.TaxAmount
                })
            })
        };
    }

    private static FacturaloPlusProviderResponse? TryDeserialize(string responseContent)
    {
        if (string.IsNullOrWhiteSpace(responseContent))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(responseContent);
            var root = document.RootElement;
            var response = new FacturaloPlusProviderResponse
            {
                Success = ReadBoolean(root, "success") ?? false,
                Code = ReadString(root, "code", "Code"),
                Message = ReadString(root, "message", "Message"),
                TrackingId = ReadString(root, "trackingId", "trackingID", "TrackingId"),
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

    private static string BuildRawResponseSummary(
        HttpStatusCode statusCode,
        string providerOperation,
        string? requestUri,
        FacturaloPlusProviderResponse? providerResponse,
        string responseContent)
    {
        var summary = new
        {
            StatusCode = (int)statusCode,
            ProviderOperation = providerOperation,
            RequestUri = requestUri,
            providerResponse?.Success,
            providerResponse?.Code,
            providerResponse?.Message,
            providerResponse?.TrackingId,
            providerResponse?.Uuid,
            providerResponse?.StampedAtUtc,
            providerResponse?.HasSuccessfulStampEvidence,
            providerResponse?.ErrorCode,
            providerResponse?.ErrorMessage,
            BodyPreview = responseContent.Length <= 500 ? responseContent : responseContent[..500]
        };

        return JsonSerializer.Serialize(summary, JsonOptions);
    }

    private static string ComputeSha256(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static string NormalizeRelativePath(string path)
    {
        return path.Trim().TrimStart('/');
    }

    private static CertificateMetadata ExtractCertificateMetadata(string certificatePem)
    {
        var certificate = X509Certificate2.CreateFromPem(certificatePem);
        var certificateBytes = certificate.Export(X509ContentType.Cert);
        var serialBytes = certificate.GetSerialNumber().Reverse().ToArray();
        var noCertificado = serialBytes.All(static x => x is >= (byte)'0' and <= (byte)'9')
            ? Encoding.ASCII.GetString(serialBytes)
            : Convert.ToHexString(serialBytes);

        return new CertificateMetadata(noCertificado, Convert.ToBase64String(certificateBytes));
    }

    private static bool TryBuildCfdiLocalDateTime(
        DateTime utcDateTime,
        out string? cfdiLocalDateTime,
        out string? validationError)
    {
        cfdiLocalDateTime = null;
        validationError = null;

        var cfdiTimeZone = ResolveCfdiTimeZone();
        if (cfdiTimeZone is null)
        {
            validationError = "CFDI emission time zone 'America/Mexico_City' could not be resolved.";
            return false;
        }

        var normalizedIssuedAtUtc = NormalizeUtc(utcDateTime);
        var localIssuedAt = TrimToSeconds(TimeZoneInfo.ConvertTimeFromUtc(normalizedIssuedAtUtc, cfdiTimeZone));
        var localNow = TrimToSeconds(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, cfdiTimeZone));

        if (localIssuedAt > localNow.Add(FutureCfdiFechaTolerance))
        {
            validationError = $"Payment complement UTC date resolves to a future local CFDI date for '{cfdiTimeZone.Id}'.";
            return false;
        }

        cfdiLocalDateTime = localIssuedAt.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
        return true;
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
        return new DateTime(value.Year, value.Month, value.Day, value.Hour, value.Minute, value.Second, value.Kind);
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

    private static decimal NormalizeMoney(decimal amount)
    {
        return Math.Round(amount, 2, MidpointRounding.AwayFromZero);
    }

    private static string FormatRate(decimal rate)
    {
        return Math.Round(rate, 6, MidpointRounding.AwayFromZero).ToString("0.000000", CultureInfo.InvariantCulture);
    }

    private static PaymentComplementStampingGatewayResult ValidationFailed(string errorMessage)
    {
        return new PaymentComplementStampingGatewayResult
        {
            Outcome = PaymentComplementStampingGatewayOutcome.ValidationFailed,
            ProviderName = "FacturaloPlus",
            ProviderOperation = "payment-complement-stamp",
            ErrorMessage = errorMessage
        };
    }

    private static PaymentComplementStampingGatewayResult Unavailable(string providerRequestHash, string errorMessage)
    {
        return new PaymentComplementStampingGatewayResult
        {
            Outcome = PaymentComplementStampingGatewayOutcome.Unavailable,
            ProviderName = "FacturaloPlus",
            ProviderOperation = "payment-complement-stamp",
            ProviderRequestHash = providerRequestHash,
            ErrorMessage = errorMessage
        };
    }

    private static bool IsProviderSuccess(FacturaloPlusProviderResponse? response)
    {
        return response?.Success == true
            || string.Equals(response?.Code, "200", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasSuccessfulStampEvidence(FacturaloPlusProviderResponse? response)
    {
        return response is not null
            && (response.HasSuccessfulStampEvidence
                || !string.IsNullOrWhiteSpace(response.Uuid));
    }

    private static void PopulateFromProviderData(JsonElement dataElement, FacturaloPlusProviderResponse response)
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

        response.Uuid ??= ReadString(dataElement, "uuid", "UUID", "Uuid", "folioFiscal", "FolioFiscal");
        response.StampedAtUtc ??= ReadUtcDateTime(dataElement, "stampedAtUtc", "StampedAtUtc", "fechaTimbrado", "FechaTimbrado");
        response.XmlContent ??= ReadString(dataElement, "xmlContent", "xml", "XML", "XmlContent");
        response.OriginalString ??= ReadString(dataElement, "originalString", "OriginalString", "cadenaOriginal", "CadenaOriginal", "cadenaOriginalTFD", "CadenaOriginalTFD");
        response.QrCodeTextOrUrl ??= ReadString(dataElement, "qrCodeTextOrUrl", "QrCodeTextOrUrl", "qrCode", "QRCode", "qr", "QR");
    }

    private static void PopulateStampEvidenceFromXml(FacturaloPlusProviderResponse response)
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

        foreach (var timeZoneId in new[] { "America/Mexico_City", "Central Standard Time (Mexico)" })
        {
            try
            {
                var cfdiTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                var unspecifiedLocalDateTime = DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified);
                return TimeZoneInfo.ConvertTimeToUtc(unspecifiedLocalDateTime, cfdiTimeZone);
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

        if (ContainsExplicitOffset(value) && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var offsetValue))
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

    private sealed class FacturaloPlusPaymentComplementStampingPayload
    {
        [JsonPropertyName("Comprobante")]
        public FacturaloPlusPaymentComplementComprobante Comprobante { get; init; } = new();
    }

    private sealed class FacturaloPlusPaymentComplementComprobante
    {
        [JsonPropertyName("Version")]
        public string Version { get; init; } = string.Empty;
        [JsonPropertyName("Fecha")]
        public string Fecha { get; init; } = string.Empty;
        [JsonPropertyName("Moneda")]
        public string Moneda { get; init; } = string.Empty;
        [JsonPropertyName("TipoDeComprobante")]
        public string TipoDeComprobante { get; init; } = string.Empty;
        [JsonPropertyName("Exportacion")]
        public string Exportacion { get; init; } = string.Empty;
        [JsonPropertyName("LugarExpedicion")]
        public string LugarExpedicion { get; init; } = string.Empty;
        [JsonPropertyName("SubTotal")]
        public decimal SubTotal { get; init; }
        [JsonPropertyName("Total")]
        public decimal Total { get; init; }
        [JsonPropertyName("NoCertificado")]
        public string NoCertificado { get; init; } = string.Empty;
        [JsonPropertyName("Certificado")]
        public string Certificado { get; init; } = string.Empty;
        [JsonPropertyName("Emisor")]
        public FacturaloPlusPaymentComplementComprobanteEmisor Emisor { get; init; } = new();
        [JsonPropertyName("Receptor")]
        public FacturaloPlusPaymentComplementComprobanteReceptor Receptor { get; init; } = new();
        [JsonPropertyName("Conceptos")]
        public List<FacturaloPlusPaymentComplementConcepto> Conceptos { get; init; } = [];
        [JsonPropertyName("Complemento")]
        public FacturaloPlusPaymentComplementComplemento Complemento { get; init; } = new();
    }

    private sealed class FacturaloPlusPaymentComplementComprobanteEmisor
    {
        [JsonPropertyName("Rfc")]
        public string Rfc { get; init; } = string.Empty;
        [JsonPropertyName("Nombre")]
        public string Nombre { get; init; } = string.Empty;
        [JsonPropertyName("RegimenFiscal")]
        public string RegimenFiscal { get; init; } = string.Empty;
    }

    private sealed class FacturaloPlusPaymentComplementComprobanteReceptor
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

    private sealed class FacturaloPlusPaymentComplementConcepto
    {
        [JsonPropertyName("ClaveProdServ")]
        public string ClaveProdServ { get; init; } = string.Empty;
        [JsonPropertyName("Cantidad")]
        public decimal Cantidad { get; init; }
        [JsonPropertyName("ClaveUnidad")]
        public string ClaveUnidad { get; init; } = string.Empty;
        [JsonPropertyName("Descripcion")]
        public string Descripcion { get; init; } = string.Empty;
        [JsonPropertyName("ValorUnitario")]
        public decimal ValorUnitario { get; init; }
        [JsonPropertyName("Importe")]
        public decimal Importe { get; init; }
        [JsonPropertyName("ObjetoImp")]
        public string ObjetoImp { get; init; } = string.Empty;
    }

    private sealed class FacturaloPlusPaymentComplementComplemento
    {
        [JsonPropertyName("Pagos20")]
        public FacturaloPlusPaymentComplementPagos20 Pagos20 { get; init; } = new();
    }

    private sealed class FacturaloPlusPaymentComplementPagos20
    {
        [JsonPropertyName("Version")]
        public string Version { get; init; } = string.Empty;
        [JsonPropertyName("Totales")]
        public FacturaloPlusPagoTotales Totales { get; init; } = new();
        [JsonPropertyName("Pago")]
        public List<FacturaloPlusPago> Pago { get; init; } = [];
    }

    private sealed class FacturaloPlusPagoTotales
    {
        [JsonPropertyName("MontoTotalPagos")]
        public decimal MontoTotalPagos { get; init; }
        [JsonPropertyName("TotalTrasladosBaseIVA16")]
        public decimal? TotalTrasladosBaseIVA16 { get; set; }
        [JsonPropertyName("TotalTrasladosImpuestoIVA16")]
        public decimal? TotalTrasladosImpuestoIVA16 { get; set; }
        [JsonPropertyName("TotalTrasladosBaseIVA8")]
        public decimal? TotalTrasladosBaseIVA8 { get; set; }
        [JsonPropertyName("TotalTrasladosImpuestoIVA8")]
        public decimal? TotalTrasladosImpuestoIVA8 { get; set; }
        [JsonPropertyName("TotalTrasladosBaseIVA0")]
        public decimal? TotalTrasladosBaseIVA0 { get; set; }
        [JsonPropertyName("TotalTrasladosImpuestoIVA0")]
        public decimal? TotalTrasladosImpuestoIVA0 { get; set; }
    }

    private sealed class FacturaloPlusPago
    {
        [JsonPropertyName("FechaPago")]
        public string FechaPago { get; init; } = string.Empty;
        [JsonPropertyName("FormaDePagoP")]
        public string FormaDePagoP { get; init; } = string.Empty;
        [JsonPropertyName("MonedaP")]
        public string MonedaP { get; init; } = string.Empty;
        [JsonPropertyName("Monto")]
        public decimal Monto { get; init; }
        [JsonPropertyName("ImpuestosP")]
        public FacturaloPlusPagoImpuestosP? ImpuestosP { get; init; }
        [JsonPropertyName("DoctoRelacionado")]
        public List<FacturaloPlusPagoDoctoRelacionado> DoctoRelacionado { get; init; } = [];
    }

    private sealed class FacturaloPlusPagoImpuestosP
    {
        [JsonPropertyName("TrasladosP")]
        public List<FacturaloPlusPagoTrasladoP> TrasladosP { get; init; } = [];
    }

    private sealed class FacturaloPlusPagoTrasladoP
    {
        [JsonPropertyName("BaseP")]
        public decimal BaseP { get; init; }
        [JsonPropertyName("ImpuestoP")]
        public string ImpuestoP { get; init; } = string.Empty;
        [JsonPropertyName("TipoFactorP")]
        public string TipoFactorP { get; init; } = string.Empty;
        [JsonPropertyName("TasaOCuotaP")]
        public string TasaOCuotaP { get; init; } = string.Empty;
        [JsonPropertyName("ImporteP")]
        public decimal ImporteP { get; init; }
    }

    private sealed class FacturaloPlusPagoDoctoRelacionado
    {
        [JsonPropertyName("IdDocumento")]
        public string IdDocumento { get; init; } = string.Empty;
        [JsonPropertyName("MonedaDR")]
        public string MonedaDR { get; init; } = string.Empty;
        [JsonPropertyName("MetodoDePagoDR")]
        public string MetodoDePagoDR { get; init; } = string.Empty;
        [JsonPropertyName("NumParcialidad")]
        public int NumParcialidad { get; init; }
        [JsonPropertyName("ImpSaldoAnt")]
        public decimal ImpSaldoAnt { get; init; }
        [JsonPropertyName("ImpPagado")]
        public decimal ImpPagado { get; init; }
        [JsonPropertyName("ImpSaldoInsoluto")]
        public decimal ImpSaldoInsoluto { get; init; }
        [JsonPropertyName("ObjetoImpDR")]
        public string ObjetoImpDR { get; init; } = string.Empty;
        [JsonPropertyName("ImpuestosDR")]
        public FacturaloPlusPagoImpuestosDR? ImpuestosDR { get; init; }
    }

    private sealed class FacturaloPlusPagoImpuestosDR
    {
        [JsonPropertyName("TrasladosDR")]
        public List<FacturaloPlusPagoTrasladoDR>? TrasladosDR { get; init; }
    }

    private sealed class FacturaloPlusPagoTrasladoDR
    {
        [JsonPropertyName("BaseDR")]
        public decimal BaseDR { get; init; }
        [JsonPropertyName("ImpuestoDR")]
        public string ImpuestoDR { get; init; } = string.Empty;
        [JsonPropertyName("TipoFactorDR")]
        public string TipoFactorDR { get; init; } = string.Empty;
        [JsonPropertyName("TasaOCuotaDR")]
        public string TasaOCuotaDR { get; init; } = string.Empty;
        [JsonPropertyName("ImporteDR")]
        public decimal ImporteDR { get; init; }
    }

    private sealed record CertificateMetadata(string NoCertificado, string Certificado);

    private sealed class FacturaloPlusProviderResponse
    {
        public bool Success { get; set; }

        public string? Code { get; set; }

        public string? Message { get; set; }

        public string? TrackingId { get; set; }

        public string? Uuid { get; set; }

        public DateTime? StampedAtUtc { get; set; }

        public string? XmlContent { get; set; }

        public string? OriginalString { get; set; }

        public string? QrCodeTextOrUrl { get; set; }

        public string? ErrorCode { get; set; }

        public string? ErrorMessage { get; set; }

        public bool HasSuccessfulStampEvidence { get; set; }
    }
}
