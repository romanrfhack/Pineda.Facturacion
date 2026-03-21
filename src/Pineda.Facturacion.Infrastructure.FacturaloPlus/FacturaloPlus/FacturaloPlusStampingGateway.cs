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
        var itemPayloads = request.Items
            .OrderBy(x => x.LineNumber)
            .Select(BuildConcepto)
            .ToList();

        var traslados = itemPayloads
            .Where(x => x.Impuestos?.Traslados is { Count: > 0 })
            .SelectMany(x => x.Impuestos!.Traslados!)
            .GroupBy(x => new { x.Impuesto, x.TipoFactor, x.TasaOCuota })
            .Select(group => new FacturaloPlusComprobanteTraslado
            {
                Base = group.Sum(x => x.Base),
                Impuesto = group.Key.Impuesto,
                TipoFactor = group.Key.TipoFactor,
                TasaOCuota = group.Key.TasaOCuota,
                Importe = group.Sum(x => x.Importe)
            })
            .ToList();

        return new FacturaloPlusStampingPayload
        {
            Comprobante = new FacturaloPlusComprobante
            {
                Version = request.CfdiVersion,
                Serie = request.Series,
                Folio = request.Folio,
                Fecha = request.IssuedAtUtc,
                Moneda = request.CurrencyCode,
                TipoDeComprobante = request.DocumentType,
                MetodoPago = request.PaymentMethodSat,
                FormaPago = request.PaymentFormSat,
                CondicionesDePago = request.PaymentCondition,
                Exportacion = "01",
                TipoCambio = request.ExchangeRate == 1m ? null : request.ExchangeRate,
                SubTotal = request.Subtotal,
                Descuento = request.DiscountTotal > 0 ? request.DiscountTotal : null,
                Total = request.Total,
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
                        TotalImpuestosTrasladados = traslados.Sum(x => x.Importe),
                        Traslados = traslados
                    }
            }
        };
    }

    private static FacturaloPlusComprobanteConcepto BuildConcepto(FiscalStampingRequestItem item)
    {
        var traslados = BuildConceptoTraslados(item);

        return new FacturaloPlusComprobanteConcepto
        {
            ClaveProdServ = item.SatProductServiceCode,
            NoIdentificacion = item.InternalCode,
            Cantidad = item.Quantity,
            ClaveUnidad = item.SatUnitCode,
            Unidad = item.UnitText,
            Descripcion = item.Description,
            ValorUnitario = item.UnitPrice,
            Importe = item.Subtotal,
            Descuento = item.DiscountAmount > 0 ? item.DiscountAmount : null,
            ObjetoImp = item.TaxObjectCode,
            Impuestos = traslados.Count == 0
                ? null
                : new FacturaloPlusComprobanteConceptoImpuestos
                {
                    Traslados = traslados
                }
        };
    }

    private static List<FacturaloPlusComprobanteTraslado> BuildConceptoTraslados(FiscalStampingRequestItem item)
    {
        if (item.TaxTotal <= 0 || item.VatRate <= 0)
        {
            return [];
        }

        return
        [
            new FacturaloPlusComprobanteTraslado
            {
                Base = item.Subtotal,
                Impuesto = "002",
                TipoFactor = "Tasa",
                TasaOCuota = item.VatRate,
                Importe = item.TaxTotal
            }
        ];
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
        public DateTime Fecha { get; init; }
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
        [JsonPropertyName("SubTotal")]
        public decimal SubTotal { get; init; }
        [JsonPropertyName("Descuento")]
        public decimal? Descuento { get; init; }
        [JsonPropertyName("Total")]
        public decimal Total { get; init; }
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
        public decimal TasaOCuota { get; init; }
        [JsonPropertyName("Importe")]
        public decimal Importe { get; init; }
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
