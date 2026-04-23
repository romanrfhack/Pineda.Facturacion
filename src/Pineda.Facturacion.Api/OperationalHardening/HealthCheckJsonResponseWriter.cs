using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Pineda.Facturacion.Api.OperationalHardening;

internal static class HealthCheckJsonResponseWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(report);

        context.Response.ContentType = "application/json";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration,
            results = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new
                {
                    status = entry.Value.Status.ToString(),
                    description = entry.Value.Description,
                    duration = entry.Value.Duration,
                    tags = entry.Value.Tags
                },
                StringComparer.Ordinal)
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload, SerializerOptions));
    }

    public static HealthCheckOptions CreateOptions(string tag)
    {
        return new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(tag, StringComparer.Ordinal),
            ResponseWriter = WriteAsync
        };
    }
}
