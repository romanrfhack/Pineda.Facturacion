using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
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

        var privateKeyPasswordValue = await _secretReferenceResolver.ResolveAsync(request.PrivateKeyPasswordReference, cancellationToken);
        if (string.IsNullOrWhiteSpace(privateKeyPasswordValue))
        {
            return ValidationFailed("Payment complement private key password reference could not be resolved.");
        }

        var redactedSummary = BuildRedactedRequestSummary(request);
        var providerRequestHash = ComputeSha256(JsonSerializer.Serialize(redactedSummary, JsonOptions));
        var payload = BuildPayload(request, certificateValue, privateKeyValue, privateKeyPasswordValue);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _options.PaymentComplementStampPath)
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };

        if (!string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(_options.ApiKeyHeaderName))
        {
            httpRequest.Headers.TryAddWithoutValidation(_options.ApiKeyHeaderName, apiKey);
        }

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
            return new PaymentComplementStampingGatewayResult
            {
                Outcome = PaymentComplementStampingGatewayOutcome.Stamped,
                ProviderName = _options.ProviderName,
                ProviderOperation = "payment-complement-stamp",
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
                ProviderOperation = "payment-complement-stamp",
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
            ProviderOperation = "payment-complement-stamp",
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
        string certificateValue,
        string privateKeyValue,
        string privateKeyPasswordValue)
    {
        return new FacturaloPlusPaymentComplementStampingPayload
        {
            Environment = request.PacEnvironment,
            CfdiVersion = request.CfdiVersion,
            DocumentType = request.DocumentType,
            IssuedAtUtc = request.IssuedAtUtc,
            PaymentDateUtc = request.PaymentDateUtc,
            CurrencyCode = request.CurrencyCode,
            TotalPaymentsAmount = request.TotalPaymentsAmount,
            Issuer = new FacturaloPlusPaymentComplementParty
            {
                Rfc = request.IssuerRfc,
                LegalName = request.IssuerLegalName,
                FiscalRegimeCode = request.IssuerFiscalRegimeCode,
                PostalCode = request.IssuerPostalCode
            },
            Receiver = new FacturaloPlusPaymentComplementParty
            {
                Rfc = request.ReceiverRfc,
                LegalName = request.ReceiverLegalName,
                FiscalRegimeCode = request.ReceiverFiscalRegimeCode,
                PostalCode = request.ReceiverPostalCode,
                CountryCode = request.ReceiverCountryCode,
                ForeignTaxRegistration = request.ReceiverForeignTaxRegistration
            },
            RelatedDocuments = request.RelatedDocuments
                .Select(x => new FacturaloPlusPaymentComplementRelatedDocument
                {
                    AccountsReceivableInvoiceId = x.AccountsReceivableInvoiceId,
                    FiscalDocumentId = x.FiscalDocumentId,
                    RelatedDocumentUuid = x.RelatedDocumentUuid,
                    InstallmentNumber = x.InstallmentNumber,
                    PreviousBalance = x.PreviousBalance,
                    PaidAmount = x.PaidAmount,
                    RemainingBalance = x.RemainingBalance,
                    CurrencyCode = x.CurrencyCode
                })
                .ToList(),
            Certificate = new FacturaloPlusCertificateMaterial
            {
                Certificate = certificateValue,
                PrivateKey = privateKeyValue,
                PrivateKeyPassword = privateKeyPasswordValue
            }
        };
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
                x.CurrencyCode
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

    private static string BuildRawResponseSummary(HttpStatusCode statusCode, FacturaloPlusProviderResponse? providerResponse, string responseContent)
    {
        var summary = new
        {
            StatusCode = (int)statusCode,
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
        public string Environment { get; set; } = string.Empty;

        public string CfdiVersion { get; set; } = string.Empty;

        public string DocumentType { get; set; } = string.Empty;

        public DateTime IssuedAtUtc { get; set; }

        public DateTime PaymentDateUtc { get; set; }

        public string CurrencyCode { get; set; } = string.Empty;

        public decimal TotalPaymentsAmount { get; set; }

        public FacturaloPlusPaymentComplementParty Issuer { get; set; } = new();

        public FacturaloPlusPaymentComplementParty Receiver { get; set; } = new();

        public List<FacturaloPlusPaymentComplementRelatedDocument> RelatedDocuments { get; set; } = [];

        public FacturaloPlusCertificateMaterial Certificate { get; set; } = new();
    }

    private sealed class FacturaloPlusPaymentComplementParty
    {
        public string Rfc { get; set; } = string.Empty;

        public string LegalName { get; set; } = string.Empty;

        public string FiscalRegimeCode { get; set; } = string.Empty;

        public string PostalCode { get; set; } = string.Empty;

        public string? CountryCode { get; set; }

        public string? ForeignTaxRegistration { get; set; }
    }

    private sealed class FacturaloPlusPaymentComplementRelatedDocument
    {
        public long AccountsReceivableInvoiceId { get; set; }

        public long? FiscalDocumentId { get; set; }

        public string RelatedDocumentUuid { get; set; } = string.Empty;

        public int InstallmentNumber { get; set; }

        public decimal PreviousBalance { get; set; }

        public decimal PaidAmount { get; set; }

        public decimal RemainingBalance { get; set; }

        public string CurrencyCode { get; set; } = string.Empty;
    }

    private sealed class FacturaloPlusCertificateMaterial
    {
        public string Certificate { get; set; } = string.Empty;

        public string PrivateKey { get; set; } = string.Empty;

        public string PrivateKeyPassword { get; set; } = string.Empty;
    }

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
