using System.Globalization;
using System.Net;
using System.Net.Http.Json;
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

        var apiKey = await ResolveApiKeyAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(_options.ApiKeyReference) && string.IsNullOrWhiteSpace(apiKey))
        {
            return ValidationFailed("Configured PAC API key reference could not be resolved.");
        }

        var payload = new FacturaloPlusStatusQueryPayload
        {
            Uuid = request.Uuid,
            IssuerRfc = request.IssuerRfc,
            ReceiverRfc = request.ReceiverRfc,
            Total = request.Total
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _options.StatusQueryPath)
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
            return Unavailable("Provider timeout.");
        }
        catch (HttpRequestException)
        {
            return Unavailable("Provider transport failure.");
        }

        var providerResponse = TryDeserialize(responseContent);
        var rawResponseSummaryJson = BuildRawResponseSummary(response.StatusCode, providerResponse, responseContent);

        if (response.IsSuccessStatusCode && providerResponse is not null)
        {
            return new FiscalStatusQueryGatewayResult
            {
                Outcome = FiscalStatusQueryGatewayOutcome.Refreshed,
                ProviderName = _options.ProviderName,
                ProviderOperation = "status-query",
                ProviderTrackingId = providerResponse.TrackingId,
                ProviderCode = providerResponse.Code,
                ProviderMessage = providerResponse.Message,
                ExternalStatus = providerResponse.ExternalStatus,
                CheckedAtUtc = providerResponse.CheckedAtUtc ?? DateTime.UtcNow,
                RawResponseSummaryJson = rawResponseSummaryJson
            };
        }

        if ((int)response.StatusCode >= 500)
        {
            return new FiscalStatusQueryGatewayResult
            {
                Outcome = FiscalStatusQueryGatewayOutcome.Unavailable,
                ProviderName = _options.ProviderName,
                ProviderOperation = "status-query",
                ProviderCode = providerResponse?.Code ?? ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture),
                ProviderMessage = providerResponse?.Message,
                CheckedAtUtc = DateTime.UtcNow,
                RawResponseSummaryJson = rawResponseSummaryJson,
                ErrorCode = providerResponse?.ErrorCode ?? "HTTP_" + (int)response.StatusCode,
                ErrorMessage = providerResponse?.ErrorMessage ?? "Provider unavailable."
            };
        }

        return new FiscalStatusQueryGatewayResult
        {
            Outcome = FiscalStatusQueryGatewayOutcome.Refreshed,
            ProviderName = _options.ProviderName,
            ProviderOperation = "status-query",
            ProviderCode = providerResponse?.Code ?? ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture),
            ProviderMessage = providerResponse?.Message,
            ExternalStatus = providerResponse?.ExternalStatus,
            CheckedAtUtc = providerResponse?.CheckedAtUtc ?? DateTime.UtcNow,
            RawResponseSummaryJson = rawResponseSummaryJson
        };
    }

    private async Task<string?> ResolveApiKeyAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKeyReference))
        {
            return null;
        }

        return await _secretReferenceResolver.ResolveAsync(_options.ApiKeyReference, cancellationToken);
    }

    private static string BuildRawResponseSummary(HttpStatusCode statusCode, FacturaloPlusStatusQueryResponse? response, string rawContent)
    {
        var summary = new
        {
            HttpStatusCode = (int)statusCode,
            response?.TrackingId,
            response?.Code,
            response?.Message,
            response?.ExternalStatus,
            response?.CheckedAtUtc,
            response?.ErrorCode,
            response?.ErrorMessage,
            RawContentPreview = string.IsNullOrWhiteSpace(rawContent)
                ? null
                : rawContent.Length <= 500 ? rawContent : rawContent[..500]
        };

        return JsonSerializer.Serialize(summary, JsonOptions);
    }

    private static FacturaloPlusStatusQueryResponse? TryDeserialize(string rawContent)
    {
        if (string.IsNullOrWhiteSpace(rawContent))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<FacturaloPlusStatusQueryResponse>(rawContent, JsonOptions);
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
            ProviderOperation = "status-query",
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
            ProviderOperation = "status-query",
            CheckedAtUtc = DateTime.UtcNow,
            ErrorMessage = errorMessage,
            RawResponseSummaryJson = JsonSerializer.Serialize(new { error = errorMessage }, JsonOptions)
        };
    }

    private sealed class FacturaloPlusStatusQueryPayload
    {
        public string Uuid { get; init; } = string.Empty;
        public string IssuerRfc { get; init; } = string.Empty;
        public string ReceiverRfc { get; init; } = string.Empty;
        public decimal Total { get; init; }
    }

    private sealed class FacturaloPlusStatusQueryResponse
    {
        public string? TrackingId { get; init; }
        public string? Code { get; init; }
        public string? Message { get; init; }
        public string? ExternalStatus { get; init; }
        public DateTime? CheckedAtUtc { get; init; }
        public string? ErrorCode { get; init; }
        public string? ErrorMessage { get; init; }
    }
}
