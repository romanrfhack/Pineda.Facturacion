using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Pineda.Facturacion.Application.Abstractions.Pac;
using Pineda.Facturacion.Application.Abstractions.Secrets;
using Pineda.Facturacion.Application.Contracts.Pac;
using Pineda.Facturacion.Infrastructure.FacturaloPlus.Options;

namespace Pineda.Facturacion.Infrastructure.FacturaloPlus.FacturaloPlus;

public class FacturaloPlusStatusQueryGateway : IFiscalStatusQueryGateway
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly FacturaloPlusOptions _options;
    private readonly ISecretReferenceResolver _secretReferenceResolver;

    public FacturaloPlusStatusQueryGateway(
        HttpClient httpClient,
        IOptions<FacturaloPlusOptions> options,
        ISecretReferenceResolver secretReferenceResolver)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _secretReferenceResolver = secretReferenceResolver;
    }

    public async Task<FiscalStatusQueryGatewayResult> QueryStatusAsync(
        FiscalStatusQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var apiKey = await ResolveRequiredSecretAsync(_options.ApiKeyReference, cancellationToken);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return ValidationFailed("Configured PAC API key reference could not be resolved.");
        }

        var formPayload = new List<KeyValuePair<string, string>>
        {
            new("apikey", apiKey),
            new("uuid", request.Uuid),
            new("rfcEmisor", request.IssuerRfc),
            new("rfcReceptor", request.ReceiverRfc),
            new("total", request.Total.ToString("0.######", CultureInfo.InvariantCulture))
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _options.StatusQueryPath)
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
            return Unavailable("Provider timeout.");
        }
        catch (HttpRequestException)
        {
            return Unavailable("Provider transport failure.");
        }

        var providerResponse = TryParseProviderResponse(responseContent);
        var rawResponseSummaryJson = BuildRawResponseSummary(response.StatusCode, providerResponse, responseContent);

        if ((int)response.StatusCode >= 500)
        {
            var providerCode = ExtractCompactCodigoEstatus(providerResponse?.CodigoEstatus)
                ?? providerResponse?.ErrorCode
                ?? ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture);
            return new FiscalStatusQueryGatewayResult
            {
                Outcome = FiscalStatusQueryGatewayOutcome.Unavailable,
                ProviderName = _options.ProviderName,
                ProviderOperation = "consultarEstadoSAT",
                ProviderTrackingId = providerResponse?.TrackingId,
                ProviderCode = providerCode,
                ProviderMessage = BuildProviderMessage(providerResponse),
                CheckedAtUtc = DateTime.UtcNow,
                RawResponseSummaryJson = rawResponseSummaryJson,
                ErrorCode = providerResponse?.ErrorCode ?? "HTTP_" + (int)response.StatusCode,
                ErrorMessage = providerResponse?.ErrorMessage ?? "Provider unavailable."
            };
        }

        if (providerResponse is null)
        {
            return new FiscalStatusQueryGatewayResult
            {
                Outcome = FiscalStatusQueryGatewayOutcome.ValidationFailed,
                ProviderName = _options.ProviderName,
                ProviderOperation = "consultarEstadoSAT",
                CheckedAtUtc = DateTime.UtcNow,
                ErrorMessage = "Provider status query response could not be parsed.",
                RawResponseSummaryJson = rawResponseSummaryJson
            };
        }

        var compactProviderCode = ExtractCompactCodigoEstatus(providerResponse.CodigoEstatus) ?? providerResponse.CodigoEstatus;

        return new FiscalStatusQueryGatewayResult
        {
            Outcome = FiscalStatusQueryGatewayOutcome.Refreshed,
            ProviderName = _options.ProviderName,
            ProviderOperation = "consultarEstadoSAT",
            ProviderTrackingId = providerResponse.TrackingId,
            ProviderCode = compactProviderCode,
            ProviderMessage = BuildProviderMessage(providerResponse),
            ExternalStatus = providerResponse.Estado,
            Cancelability = providerResponse.EsCancelable,
            CancellationStatus = providerResponse.EstatusCancelacion,
            CheckedAtUtc = DateTime.UtcNow,
            RawResponseSummaryJson = rawResponseSummaryJson
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

    private static string BuildRawResponseSummary(HttpStatusCode statusCode, FacturaloPlusStatusQueryResponse? response, string rawContent)
    {
        var summary = new
        {
            HttpStatusCode = (int)statusCode,
            response?.CodigoEstatus,
            response?.EsCancelable,
            response?.Estado,
            response?.EstatusCancelacion,
            response?.TrackingId,
            response?.ErrorCode,
            response?.ErrorMessage,
            RawContentPreview = string.IsNullOrWhiteSpace(rawContent)
                ? null
                : rawContent.Length <= 500 ? rawContent : rawContent[..500]
        };

        return JsonSerializer.Serialize(summary, JsonOptions);
    }

    private static FacturaloPlusStatusQueryResponse? TryParseProviderResponse(string rawContent)
    {
        if (string.IsNullOrWhiteSpace(rawContent))
        {
            return null;
        }

        try
        {
            using var json = JsonDocument.Parse(rawContent);
            var root = json.RootElement;
            return new FacturaloPlusStatusQueryResponse
            {
                CodigoEstatus = ReadString(root, "CodigoEstatus", "codigoEstatus", "code", "codigo"),
                EsCancelable = ReadString(root, "EsCancelable", "esCancelable"),
                Estado = ReadString(root, "Estado", "estado"),
                EstatusCancelacion = ReadString(root, "EstatusCancelacion", "estatusCancelacion"),
                TrackingId = ReadString(root, "trackingId", "tracking", "folio", "id"),
                ErrorCode = ReadString(root, "errorCode", "codigoError"),
                ErrorMessage = ReadString(root, "errorMessage", "mensajeError", "descripcionError", "message", "mensaje")
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private FiscalStatusQueryGatewayResult ValidationFailed(string errorMessage)
    {
        return new FiscalStatusQueryGatewayResult
        {
            Outcome = FiscalStatusQueryGatewayOutcome.ValidationFailed,
            ProviderName = _options.ProviderName,
            ProviderOperation = "consultarEstadoSAT",
            CheckedAtUtc = DateTime.UtcNow,
            ErrorMessage = errorMessage,
            RawResponseSummaryJson = JsonSerializer.Serialize(new { error = errorMessage }, JsonOptions)
        };
    }

    private FiscalStatusQueryGatewayResult Unavailable(string errorMessage)
    {
        return new FiscalStatusQueryGatewayResult
        {
            Outcome = FiscalStatusQueryGatewayOutcome.Unavailable,
            ProviderName = _options.ProviderName,
            ProviderOperation = "consultarEstadoSAT",
            CheckedAtUtc = DateTime.UtcNow,
            ErrorMessage = errorMessage,
            RawResponseSummaryJson = JsonSerializer.Serialize(new { error = errorMessage }, JsonOptions)
        };
    }

    private static string? BuildProviderMessage(FacturaloPlusStatusQueryResponse? response)
    {
        if (response is null)
        {
            return null;
        }

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(response.CodigoEstatus))
        {
            parts.Add($"CodigoEstatus={response.CodigoEstatus}");
        }

        if (!string.IsNullOrWhiteSpace(response.Estado))
        {
            parts.Add($"Estado={response.Estado}");
        }

        if (!string.IsNullOrWhiteSpace(response.EsCancelable))
        {
            parts.Add($"EsCancelable={response.EsCancelable}");
        }

        if (!string.IsNullOrWhiteSpace(response.EstatusCancelacion))
        {
            parts.Add($"EstatusCancelacion={response.EstatusCancelacion}");
        }

        if (parts.Count > 0)
        {
            return string.Join(" | ", parts);
        }

        return response.ErrorMessage;
    }

    private static string? ExtractCompactCodigoEstatus(string? codigoEstatus)
    {
        if (string.IsNullOrWhiteSpace(codigoEstatus))
        {
            return null;
        }

        var normalized = codigoEstatus.Trim();
        if (normalized.StartsWith("S", StringComparison.OrdinalIgnoreCase))
        {
            return "S";
        }

        var match = System.Text.RegularExpressions.Regex.Match(
            normalized,
            @"^(N\s+\d{3})",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        if (match.Success)
        {
            return match.Groups[1].Value.ToUpperInvariant();
        }

        return normalized.Length <= 20 ? normalized : normalized[..20];
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

    private sealed class FacturaloPlusStatusQueryResponse
    {
        public string? CodigoEstatus { get; init; }
        public string? EsCancelable { get; init; }
        public string? Estado { get; init; }
        public string? EstatusCancelacion { get; init; }
        public string? TrackingId { get; init; }
        public string? ErrorCode { get; init; }
        public string? ErrorMessage { get; init; }
    }
}
