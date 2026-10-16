using System.Text.Json;

namespace Pineda.Facturacion.Operations;

public static class ServiceBillingConnectionResolver
{
    public static string? Resolve(IReadOnlyCollection<string> environment, string? productionSettingsJson)
    {
        using var settings = productionSettingsJson is null ? null : JsonDocument.Parse(productionSettingsJson,
            new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });

        string? ReadEnvironment(string key) => environment
            .FirstOrDefault(value => value.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))?[(key.Length + 1)..];

        string? ReadJson(string sectionName, string key)
        {
            if (settings?.RootElement.ValueKind != JsonValueKind.Object) return null;
            foreach (var section in settings.RootElement.EnumerateObject())
            {
                if (!section.Name.Equals(sectionName, StringComparison.OrdinalIgnoreCase) || section.Value.ValueKind != JsonValueKind.Object) continue;
                foreach (var property in section.Value.EnumerateObject())
                {
                    if (property.Name.Equals(key, StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind == JsonValueKind.String)
                        return property.Value.GetString();
                }
            }
            return null;
        }

        // Environment overrides JSON per key; the API then prefers the standard
        // ConnectionStrings key to the legacy BillingWrite key.
        var standard = ReadEnvironment("ConnectionStrings__BillingWrite") ?? ReadJson("ConnectionStrings", "BillingWrite");
        if (!string.IsNullOrWhiteSpace(standard)) return standard;
        return ReadEnvironment("BillingWrite__ConnectionString") ?? ReadJson("BillingWrite", "ConnectionString");
    }
}
