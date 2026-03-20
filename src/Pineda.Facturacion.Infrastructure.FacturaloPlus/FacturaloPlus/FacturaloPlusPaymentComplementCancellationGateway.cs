using System.Globalization;
using System.Net;
using System.Net.Http.Json;
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

public class FacturaloPlusPaymentComplementCancellationGateway : IPaymentComplementCancellationGateway
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly FacturaloPlusOptions _options;
    private readonly ISecretReferenceResolver _secretReferenceResolver;

    public FacturaloPlusPaymentComplementCancellationGateway(
        HttpClient httpClient,
        IOptions<FacturaloPlusOptions> options,
        ISecretReferenceResolver secretReferenceResolver)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _secretReferenceResolver = secretReferenceResolver;
    }

    public async Task<PaymentComplementCancellationGatewayResult> CancelAsync(PaymentComplementCancellationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var apiKey = await ResolveApiKeyAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(_options.ApiKeyReference) && string.IsNullOrWhiteSpace(apiKey))
        {
            return ValidationFailed("Configured PAC API key reference could not be resolved.");
        }

        var redactedSummary = BuildRedactedRequestSummary(request);
        var rawRequestHash = ComputeSha256(JsonSerializer.Serialize(redactedSummary, JsonOptions));
        var payload = new PaymentComplementCancellationPayload
        {
            Uuid = request.Uuid,
            IssuerRfc = request.IssuerRfc,
            ReceiverRfc = request.ReceiverRfc,
            Total = request.Total,
            CancellationReasonCode = request.CancellationReasonCode,
            ReplacementUuid = request.ReplacementUuid
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _options.PaymentComplementCancelPath)
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
            return Unavailable("Provider timeout.", rawRequestHash);
        }
        catch (HttpRequestException)
        {
            return Unavailable("Provider transport failure.", rawRequestHash);
        }

        var providerResponse = TryDeserialize(responseContent);
        var rawResponseSummaryJson = BuildRawResponseSummary(response.StatusCode, providerResponse, responseContent);

        if (response.IsSuccessStatusCode && providerResponse?.Success == true)
        {
            return new PaymentComplementCancellationGatewayResult
            {
                Outcome = PaymentComplementCancellationGatewayOutcome.Cancelled,
                ProviderName = _options.ProviderName,
                ProviderOperation = "payment-complement-cancel",
                ProviderTrackingId = providerResponse.TrackingId,
                ProviderCode = providerResponse.Code,
                ProviderMessage = providerResponse.Message,
                CancelledAtUtc = providerResponse.CancelledAtUtc ?? DateTime.UtcNow,
                RawResponseSummaryJson = rawResponseSummaryJson
            };
        }

        if ((int)response.StatusCode >= 500)
        {
            return new PaymentComplementCancellationGatewayResult
            {
                Outcome = PaymentComplementCancellationGatewayOutcome.Unavailable,
                ProviderName = _options.ProviderName,
                ProviderOperation = "payment-complement-cancel",
                ProviderTrackingId = providerResponse?.TrackingId,
                ProviderCode = providerResponse?.Code ?? ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture),
                ProviderMessage = providerResponse?.Message,
                RawResponseSummaryJson = rawResponseSummaryJson,
                ErrorCode = providerResponse?.ErrorCode ?? "HTTP_" + (int)response.StatusCode,
                ErrorMessage = providerResponse?.ErrorMessage ?? "Provider unavailable."
            };
        }

        return new PaymentComplementCancellationGatewayResult
        {
            Outcome = PaymentComplementCancellationGatewayOutcome.Rejected,
            ProviderName = _options.ProviderName,
            ProviderOperation = "payment-complement-cancel",
            ProviderTrackingId = providerResponse?.TrackingId,
            ProviderCode = providerResponse?.Code ?? ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture),
            ProviderMessage = providerResponse?.Message,
            RawResponseSummaryJson = rawResponseSummaryJson,
            ErrorCode = providerResponse?.ErrorCode,
            ErrorMessage = providerResponse?.ErrorMessage ?? providerResponse?.Message ?? "Provider rejected the cancellation request."
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

    private static object BuildRedactedRequestSummary(PaymentComplementCancellationRequest request)
    {
        return new
        {
            request.PaymentComplementId,
            request.Uuid,
            request.IssuerRfc,
            request.ReceiverRfc,
            request.Total,
            request.CancellationReasonCode,
            HasReplacementUuid = !string.IsNullOrWhiteSpace(request.ReplacementUuid)
        };
    }

    private static string BuildRawResponseSummary(HttpStatusCode statusCode, PaymentComplementCancellationResponse? response, string rawContent)
    {
        var summary = new
        {
            HttpStatusCode = (int)statusCode,
            response?.Success,
            response?.TrackingId,
            response?.Code,
            response?.Message,
            response?.CancelledAtUtc,
            response?.ErrorCode,
            response?.ErrorMessage,
            RawContentPreview = string.IsNullOrWhiteSpace(rawContent) ? null : rawContent.Length <= 500 ? rawContent : rawContent[..500]
        };

        return JsonSerializer.Serialize(summary, JsonOptions);
    }

    private static PaymentComplementCancellationResponse? TryDeserialize(string rawContent)
    {
        if (string.IsNullOrWhiteSpace(rawContent))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<PaymentComplementCancellationResponse>(rawContent, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private PaymentComplementCancellationGatewayResult ValidationFailed(string errorMessage)
    {
        return new PaymentComplementCancellationGatewayResult
        {
            Outcome = PaymentComplementCancellationGatewayOutcome.ValidationFailed,
            ProviderName = _options.ProviderName,
            ProviderOperation = "payment-complement-cancel",
            ErrorMessage = errorMessage,
            RawResponseSummaryJson = JsonSerializer.Serialize(new { error = errorMessage }, JsonOptions)
        };
    }

    private PaymentComplementCancellationGatewayResult Unavailable(string errorMessage, string rawRequestHash)
    {
        return new PaymentComplementCancellationGatewayResult
        {
            Outcome = PaymentComplementCancellationGatewayOutcome.Unavailable,
            ProviderName = _options.ProviderName,
            ProviderOperation = "payment-complement-cancel",
            ErrorMessage = errorMessage,
            ProviderMessage = errorMessage,
            RawResponseSummaryJson = JsonSerializer.Serialize(new { error = errorMessage, requestHash = rawRequestHash }, JsonOptions)
        };
    }

    private static string ComputeSha256(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private sealed class PaymentComplementCancellationPayload
    {
        public string Uuid { get; init; } = string.Empty;
        public string IssuerRfc { get; init; } = string.Empty;
        public string ReceiverRfc { get; init; } = string.Empty;
        public decimal Total { get; init; }
        public string CancellationReasonCode { get; init; } = string.Empty;
        public string? ReplacementUuid { get; init; }
    }

    private sealed class PaymentComplementCancellationResponse
    {
        public bool Success { get; init; }
        public string? TrackingId { get; init; }
        public string? Code { get; init; }
        public string? Message { get; init; }
        public DateTime? CancelledAtUtc { get; init; }
        public string? ErrorCode { get; init; }
        public string? ErrorMessage { get; init; }
    }
}
