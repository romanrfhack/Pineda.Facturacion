using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
        var payload = BuildPayload(request);
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

        if (response.IsSuccessStatusCode && providerResponse?.Success == true && !string.IsNullOrWhiteSpace(providerResponse.Uuid))
        {
            return new FiscalStampingGatewayResult
            {
                Outcome = FiscalStampingGatewayOutcome.Stamped,
                ProviderName = _options.ProviderName,
                ProviderOperation = "stamp",
                ProviderRequestHash = providerRequestHash,
                ProviderTrackingId = providerResponse.TrackingId,
                ProviderCode = providerResponse.Code,
                ProviderMessage = providerResponse.Message,
                Uuid = providerResponse.Uuid,
                StampedAtUtc = providerResponse.StampedAtUtc ?? DateTime.UtcNow,
                XmlContent = providerResponse.XmlContent,
                XmlHash = string.IsNullOrWhiteSpace(providerResponse.XmlContent) ? null : ComputeSha256(providerResponse.XmlContent),
                OriginalString = providerResponse.OriginalString,
                QrCodeTextOrUrl = providerResponse.QrCodeTextOrUrl,
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
        FiscalStampingRequest request)
    {
        return new FacturaloPlusStampingPayload
        {
            Environment = request.PacEnvironment,
            CfdiVersion = request.CfdiVersion,
            DocumentType = request.DocumentType,
            Series = request.Series,
            Folio = request.Folio,
            IssuedAtUtc = request.IssuedAtUtc,
            CurrencyCode = request.CurrencyCode,
            ExchangeRate = request.ExchangeRate,
            PaymentMethodSat = request.PaymentMethodSat,
            PaymentFormSat = request.PaymentFormSat,
            PaymentCondition = request.PaymentCondition,
            Issuer = new FacturaloPlusStampingParty
            {
                Rfc = request.IssuerRfc,
                LegalName = request.IssuerLegalName,
                FiscalRegimeCode = request.IssuerFiscalRegimeCode,
                PostalCode = request.IssuerPostalCode
            },
            Receiver = new FacturaloPlusStampingReceiver
            {
                Rfc = request.ReceiverRfc,
                LegalName = request.ReceiverLegalName,
                FiscalRegimeCode = request.ReceiverFiscalRegimeCode,
                CfdiUseCode = request.ReceiverCfdiUseCode,
                PostalCode = request.ReceiverPostalCode,
                CountryCode = request.ReceiverCountryCode,
                ForeignTaxRegistration = request.ReceiverForeignTaxRegistration
            },
            Totals = new FacturaloPlusStampingTotals
            {
                Subtotal = request.Subtotal,
                DiscountTotal = request.DiscountTotal,
                TaxTotal = request.TaxTotal,
                Total = request.Total
            },
            Items = request.Items
                .OrderBy(x => x.LineNumber)
                .Select(x => new FacturaloPlusStampingItem
                {
                    LineNumber = x.LineNumber,
                    InternalCode = x.InternalCode,
                    Description = x.Description,
                    Quantity = x.Quantity,
                    UnitPrice = x.UnitPrice,
                    DiscountAmount = x.DiscountAmount,
                    Subtotal = x.Subtotal,
                    TaxTotal = x.TaxTotal,
                    Total = x.Total,
                    SatProductServiceCode = x.SatProductServiceCode,
                    SatUnitCode = x.SatUnitCode,
                    TaxObjectCode = x.TaxObjectCode,
                    VatRate = x.VatRate,
                    UnitText = x.UnitText
                })
                .ToList(),
        };
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

    private static FacturaloPlusStampingResponse? TryDeserialize(string rawContent)
    {
        if (string.IsNullOrWhiteSpace(rawContent))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<FacturaloPlusStampingResponse>(rawContent, JsonOptions);
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

    private sealed class FacturaloPlusStampingPayload
    {
        public string Environment { get; init; } = string.Empty;
        public string CfdiVersion { get; init; } = string.Empty;
        public string DocumentType { get; init; } = string.Empty;
        public string? Series { get; init; }
        public string? Folio { get; init; }
        public DateTime IssuedAtUtc { get; init; }
        public string CurrencyCode { get; init; } = string.Empty;
        public decimal ExchangeRate { get; init; }
        public string PaymentMethodSat { get; init; } = string.Empty;
        public string PaymentFormSat { get; init; } = string.Empty;
        public string? PaymentCondition { get; init; }
        public FacturaloPlusStampingParty Issuer { get; init; } = new();
        public FacturaloPlusStampingReceiver Receiver { get; init; } = new();
        public FacturaloPlusStampingTotals Totals { get; init; } = new();
        public List<FacturaloPlusStampingItem> Items { get; init; } = [];
    }

    private class FacturaloPlusStampingParty
    {
        public string Rfc { get; init; } = string.Empty;
        public string LegalName { get; init; } = string.Empty;
        public string FiscalRegimeCode { get; init; } = string.Empty;
        public string PostalCode { get; init; } = string.Empty;
    }

    private sealed class FacturaloPlusStampingReceiver : FacturaloPlusStampingParty
    {
        public string CfdiUseCode { get; init; } = string.Empty;
        public string? CountryCode { get; init; }
        public string? ForeignTaxRegistration { get; init; }
    }

    private sealed class FacturaloPlusStampingTotals
    {
        public decimal Subtotal { get; init; }
        public decimal DiscountTotal { get; init; }
        public decimal TaxTotal { get; init; }
        public decimal Total { get; init; }
    }

    private sealed class FacturaloPlusStampingItem
    {
        public int LineNumber { get; init; }
        public string InternalCode { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public decimal Quantity { get; init; }
        public decimal UnitPrice { get; init; }
        public decimal DiscountAmount { get; init; }
        public decimal Subtotal { get; init; }
        public decimal TaxTotal { get; init; }
        public decimal Total { get; init; }
        public string SatProductServiceCode { get; init; } = string.Empty;
        public string SatUnitCode { get; init; } = string.Empty;
        public string TaxObjectCode { get; init; } = string.Empty;
        public decimal VatRate { get; init; }
        public string? UnitText { get; init; }
    }

    private sealed class FacturaloPlusStampingResponse
    {
        public bool Success { get; init; }
        public string? TrackingId { get; init; }
        public string? Code { get; init; }
        public string? Message { get; init; }
        public string? Uuid { get; init; }
        public DateTime? StampedAtUtc { get; init; }
        public string? XmlContent { get; init; }
        public string? OriginalString { get; init; }
        public string? QrCodeTextOrUrl { get; init; }
        public string? ErrorCode { get; init; }
        public string? ErrorMessage { get; init; }
    }
}
