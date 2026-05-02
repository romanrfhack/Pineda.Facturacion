using System.Net;
using System.Text.Json;
using Xunit.Sdk;

namespace Pineda.Facturacion.IntegrationTests;

internal static class IntegrationHttpTestDiagnostics
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<JsonResponse<T>> ReadJsonAsync<T>(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string operation,
        string context)
    {
        var body = await response.Content.ReadAsStringAsync();
        if (response.StatusCode != expectedStatus)
        {
            throw new XunitException(
                $"{operation} failed. ExpectedStatus={expectedStatus}. ActualStatus={response.StatusCode}. Context={context}. Body={FormatBody(body)}");
        }

        return DeserializeJson<T>(response.StatusCode, body, operation, context);
    }

    public static T Require<T>(
        T? value,
        string valueName,
        string operation,
        string context,
        string responseBody)
        where T : struct
    {
        if (value.HasValue)
        {
            return value.Value;
        }

        throw new XunitException(
            $"{operation} did not return {valueName}. Context={context}. Body={FormatBody(responseBody)}");
    }

    private static JsonResponse<T> DeserializeJson<T>(
        HttpStatusCode statusCode,
        string body,
        string operation,
        string context)
    {
        try
        {
            var value = JsonSerializer.Deserialize<T>(body, JsonOptions);
            if (value is not null)
            {
                return new JsonResponse<T>(value, body);
            }
        }
        catch (JsonException ex)
        {
            throw new XunitException(
                $"{operation} returned invalid JSON. Status={statusCode}. Context={context}. Body={FormatBody(body)}. JsonError={ex.Message}");
        }

        throw new XunitException(
            $"{operation} returned an empty JSON payload. Status={statusCode}. Context={context}. Body={FormatBody(body)}");
    }

    private static string FormatBody(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return "<empty>";
        }

        return body.Trim();
    }
}

internal readonly record struct JsonResponse<T>(T Value, string Body);
