using System.Reflection;
using System.Text.Json;
using Pineda.Facturacion.Application.UseCases.SatProductServices;

namespace Pineda.Facturacion.Infrastructure.SatCatalogs;

public sealed class SatProductServiceCatalogSeedSource
{
    private const string ResourceName = "Pineda.Facturacion.Infrastructure.Resources.sat_product_service_catalog.json";

    private readonly Lazy<SatProductServiceCatalogSeedDocument> _document;

    public SatProductServiceCatalogSeedSource()
    {
        _document = new Lazy<SatProductServiceCatalogSeedDocument>(LoadDocument, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public SatProductServiceCatalogSeedDocument GetDocument() => _document.Value;

    private static SatProductServiceCatalogSeedDocument LoadDocument()
    {
        var assembly = typeof(SatProductServiceCatalogSeedSource).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded SAT product/service catalog resource '{ResourceName}' was not found.");

        return JsonSerializer.Deserialize<SatProductServiceCatalogSeedDocument>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("SAT product/service catalog resource is empty or invalid.");
    }
}

public sealed class SatProductServiceCatalogSeedDocument
{
    public string Version { get; init; } = string.Empty;

    public IReadOnlyList<SatProductServiceCatalogSeedItem> Items { get; init; } = [];
}

public sealed class SatProductServiceCatalogSeedItem
{
    public string Code { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string? Keywords { get; init; }

    public bool IsActive { get; init; } = true;

    public string NormalizedDescription => SearchSatProductServicesService.NormalizeSearchText(Description);

    public string KeywordsNormalized => string.IsNullOrWhiteSpace(Keywords)
        ? SearchSatProductServicesService.BuildKeywordsNormalized(Description)
        : SearchSatProductServicesService.BuildKeywordsNormalized(Keywords);
}
