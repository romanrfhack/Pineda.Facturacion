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

public class FacturaloPlusCancellationGateway : IFiscalCancellationGateway
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly FacturaloPlusOptions _options;
    private readonly ISecretReferenceResolver _secretReferenceResolver;

    public FacturaloPlusCancellationGateway(
        HttpClient httpClient,
        IOptions<FacturaloPlusOptions> options,
        ISecretReferenceResolver secretReferenceResolver)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _secretReferenceResolver = secretReferenceResolver;
    }

    public async Task<FiscalCancellationGatewayResult> CancelAsync(
        FiscalCancellationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var apiKey = await ResolveRequiredSecretAsync(_options.ApiKeyReference, cancellationToken);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return ValidationFailed("Configured PAC API key reference could not be resolved.");
        }

        var privateKeyValue = await ResolveRequiredSecretAsync(request.PrivateKeyReference, cancellationToken);
        if (string.IsNullOrWhiteSpace(privateKeyValue))
        {
            return ValidationFailed("Fiscal document private key reference could not be resolved.");
        }

        var certificateValue = await ResolveRequiredSecretAsync(request.CertificateReference, cancellationToken);
        if (string.IsNullOrWhiteSpace(certificateValue))
        {
            return ValidationFailed("Fiscal document certificate reference could not be resolved.");
        }

        var privateKeyPassword = await ResolveRequiredSecretAsync(request.PrivateKeyPasswordReference, cancellationToken);
        if (string.IsNullOrWhiteSpace(privateKeyPassword))
        {
            return ValidationFailed("Fiscal document private key password reference could not be resolved.");
        }

        string privateKeyBase64;
        string certificateBase64;
        try
        {
            privateKeyBase64 = NormalizePemOrBase64(privateKeyValue);
            certificateBase64 = NormalizePemOrBase64(certificateValue);
        }
        catch (CryptographicException ex)
        {
            return ValidationFailed(ex.Message);
        }

        var redactedSummary = BuildRedactedRequestSummary(request);
        var rawRequestHash = ComputeSha256(JsonSerializer.Serialize(redactedSummary, JsonOptions));
        var formPayload = BuildFormPayload(
            apiKey,
            privateKeyBase64,
            certificateBase64,
            privateKeyPassword,
            request);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _options.CancelPath)
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
            return Unavailable("Provider timeout.", rawRequestHash);
        }
        catch (HttpRequestException)
        {
            return Unavailable("Provider transport failure.", rawRequestHash);
        }

        var providerResponse = TryParseProviderResponse(responseContent);
        var rawResponseSummaryJson = BuildRawResponseSummary(response.StatusCode, providerResponse, responseContent, redactedSummary, rawRequestHash);
        var providerCode = providerResponse?.Code ?? ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture);
        var providerMessage = providerResponse?.Message;
        var supportMessage = BuildSupportMessage(providerCode, providerMessage, providerResponse?.TrackingId, providerResponse?.ErrorCode, providerResponse?.ErrorMessage);

        if ((int)response.StatusCode >= 500)
        {
            return new FiscalCancellationGatewayResult
            {
                Outcome = FiscalCancellationGatewayOutcome.Unavailable,
                ProviderName = _options.ProviderName,
                ProviderOperation = "cancelar2",
                ProviderTrackingId = providerResponse?.TrackingId,
                ProviderCode = providerCode,
                ProviderMessage = providerMessage,
                RawResponseSummaryJson = rawResponseSummaryJson,
                ErrorCode = providerResponse?.ErrorCode ?? "HTTP_" + (int)response.StatusCode,
                ErrorMessage = providerResponse?.ErrorMessage ?? "Provider unavailable.",
                SupportMessage = supportMessage
            };
        }

        if (response.IsSuccessStatusCode && IsSuccessfulCancellationCode(providerCode))
        {
            return new FiscalCancellationGatewayResult
            {
                Outcome = FiscalCancellationGatewayOutcome.Cancelled,
                ProviderName = _options.ProviderName,
                ProviderOperation = "cancelar2",
                ProviderTrackingId = providerResponse?.TrackingId,
                ProviderCode = providerCode,
                ProviderMessage = providerMessage,
                CancelledAtUtc = providerResponse?.CancelledAtUtc ?? DateTime.UtcNow,
                RawResponseSummaryJson = rawResponseSummaryJson,
                SupportMessage = supportMessage
            };
        }

        return new FiscalCancellationGatewayResult
        {
            Outcome = FiscalCancellationGatewayOutcome.Rejected,
            ProviderName = _options.ProviderName,
            ProviderOperation = "cancelar2",
            ProviderTrackingId = providerResponse?.TrackingId,
            ProviderCode = providerCode,
            ProviderMessage = providerMessage,
            RawResponseSummaryJson = rawResponseSummaryJson,
            ErrorCode = providerResponse?.ErrorCode,
            ErrorMessage = providerResponse?.ErrorMessage ?? providerMessage ?? "Provider rejected the cancellation request.",
            SupportMessage = supportMessage
        };
    }

    private async Task<string?> ResolveRequiredSecretAsync(string? referenceKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(referenceKey))
        {
            return null;
        }

        return await _secretReferenceResolver.ResolveAsync(referenceKey, cancellationToken);
    }

    private static List<KeyValuePair<string, string>> BuildFormPayload(
        string apiKey,
        string privateKeyBase64,
        string certificateBase64,
        string privateKeyPassword,
        FiscalCancellationRequest request)
    {
        var formPayload = new List<KeyValuePair<string, string>>
        {
            new("apikey", apiKey),
            new("keyCSD", privateKeyBase64),
            new("cerCSD", certificateBase64),
            new("passCSD", privateKeyPassword),
            new("uuid", request.Uuid),
            new("rfcEmisor", request.IssuerRfc),
            new("rfcReceptor", request.ReceiverRfc),
            new("total", request.Total.ToString("0.######", CultureInfo.InvariantCulture)),
            new("motivo", request.CancellationReasonCode)
        };

        if (!string.IsNullOrWhiteSpace(request.ReplacementUuid))
        {
            formPayload.Add(new KeyValuePair<string, string>("folioSustitucion", request.ReplacementUuid));
        }

        return formPayload;
    }

    private static object BuildRedactedRequestSummary(FiscalCancellationRequest request)
    {
        return new
        {
            request.FiscalDocumentId,
            request.Uuid,
            request.IssuerRfc,
            request.ReceiverRfc,
            request.Total,
            request.CancellationReasonCode,
            HasReplacementUuid = !string.IsNullOrWhiteSpace(request.ReplacementUuid),
            HasCertificateReference = !string.IsNullOrWhiteSpace(request.CertificateReference),
            HasPrivateKeyReference = !string.IsNullOrWhiteSpace(request.PrivateKeyReference),
            HasPrivateKeyPasswordReference = !string.IsNullOrWhiteSpace(request.PrivateKeyPasswordReference)
        };
    }

    private static string BuildRawResponseSummary(
        HttpStatusCode statusCode,
        FacturaloPlusCancellationResponse? response,
        string rawContent,
        object requestSummary,
        string requestHash)
    {
        var summary = new
        {
            HttpStatusCode = (int)statusCode,
            RequestSummary = requestSummary,
            RequestHash = requestHash,
            response?.Code,
            response?.Message,
            response?.TrackingId,
            response?.CancelledAtUtc,
            response?.ErrorCode,
            response?.ErrorMessage,
            RawContentPreview = string.IsNullOrWhiteSpace(rawContent)
                ? null
                : rawContent.Length <= 500 ? rawContent : rawContent[..500]
        };

        return JsonSerializer.Serialize(summary, JsonOptions);
    }

    private static FacturaloPlusCancellationResponse? TryParseProviderResponse(string rawContent)
    {
        if (string.IsNullOrWhiteSpace(rawContent))
        {
            return null;
        }

        try
        {
            using var json = JsonDocument.Parse(rawContent);
            var root = json.RootElement;
            return new FacturaloPlusCancellationResponse
            {
                Code = ReadString(root, "code", "codigo", "CodigoEstatus", "codigoEstatus", "statusCode", "estatus", "status"),
                Message = ReadString(root, "message", "mensaje", "descripcion", "detail"),
                TrackingId = ReadString(root, "trackingId", "tracking", "folio", "id"),
                ErrorCode = ReadString(root, "errorCode", "codigoError"),
                ErrorMessage = ReadString(root, "errorMessage", "mensajeError", "descripcionError"),
                CancelledAtUtc = ReadDateTime(root, "cancelledAtUtc", "fechaCancelacion", "cancelledAt")
            };
        }
        catch (JsonException)
        {
            return new FacturaloPlusCancellationResponse
            {
                Message = rawContent.Length <= 500 ? rawContent : rawContent[..500]
            };
        }
    }

    private FiscalCancellationGatewayResult ValidationFailed(string errorMessage)
    {
        return new FiscalCancellationGatewayResult
        {
            Outcome = FiscalCancellationGatewayOutcome.ValidationFailed,
            ProviderName = _options.ProviderName,
            ProviderOperation = "cancelar2",
            ErrorMessage = errorMessage,
            RawResponseSummaryJson = JsonSerializer.Serialize(new { error = errorMessage }, JsonOptions),
            SupportMessage = errorMessage
        };
    }

    private FiscalCancellationGatewayResult Unavailable(string errorMessage, string rawRequestHash)
    {
        return new FiscalCancellationGatewayResult
        {
            Outcome = FiscalCancellationGatewayOutcome.Unavailable,
            ProviderName = _options.ProviderName,
            ProviderOperation = "cancelar2",
            ProviderMessage = errorMessage,
            ErrorMessage = errorMessage,
            RawResponseSummaryJson = JsonSerializer.Serialize(new { error = errorMessage, requestHash = rawRequestHash }, JsonOptions),
            SupportMessage = errorMessage
        };
    }

    private static bool IsSuccessfulCancellationCode(string? providerCode)
    {
        if (string.IsNullOrWhiteSpace(providerCode))
        {
            return false;
        }

        var normalizedCode = providerCode.Trim();
        return normalizedCode.StartsWith("201", StringComparison.OrdinalIgnoreCase)
            || normalizedCode.StartsWith("202", StringComparison.OrdinalIgnoreCase);
    }

    private static string? BuildSupportMessage(
        string? providerCode,
        string? providerMessage,
        string? trackingId,
        string? errorCode,
        string? errorMessage)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(providerCode))
        {
            parts.Add($"ProviderCode={providerCode.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(providerMessage))
        {
            parts.Add($"ProviderMessage={providerMessage.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(trackingId))
        {
            parts.Add($"TrackingId={trackingId.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(errorCode))
        {
            parts.Add($"ErrorCode={errorCode.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            parts.Add($"ErrorMessage={errorMessage.Trim()}");
        }

        return parts.Count > 0 ? string.Join(" | ", parts) : null;
    }

    private static string NormalizePemOrBase64(string secretValue)
    {
        var normalized = secretValue.Trim().Replace("\r", string.Empty, StringComparison.Ordinal);
        if (!normalized.Contains("-----BEGIN ", StringComparison.Ordinal))
        {
            return RemoveWhitespace(normalized);
        }

        var lines = normalized
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !line.StartsWith("-----BEGIN ", StringComparison.Ordinal) && !line.StartsWith("-----END ", StringComparison.Ordinal));
        var base64Payload = string.Concat(lines);
        if (string.IsNullOrWhiteSpace(base64Payload))
        {
            throw new CryptographicException("Fiscal document CSD secret could not be converted to base64 payload.");
        }

        return RemoveWhitespace(base64Payload);
    }

    private static string RemoveWhitespace(string value)
    {
        return new string(value.Where(ch => !char.IsWhiteSpace(ch)).ToArray());
    }

    private static string? ReadString(JsonElement root, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!TryGetPropertyIgnoreCase(root, propertyName, out var value))
            {
                continue;
            }

            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                JsonValueKind.True => bool.TrueString,
                JsonValueKind.False => bool.FalseString,
                _ => value.GetRawText()
            };
        }

        return null;
    }

    private static DateTime? ReadDateTime(JsonElement root, params string[] propertyNames)
    {
        var value = ReadString(root, propertyNames);
        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : null;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string ComputeSha256(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private sealed class FacturaloPlusCancellationResponse
    {
        public string? Code { get; init; }
        public string? Message { get; init; }
        public string? TrackingId { get; init; }
        public DateTime? CancelledAtUtc { get; init; }
        public string? ErrorCode { get; init; }
        public string? ErrorMessage { get; init; }
    }
}
