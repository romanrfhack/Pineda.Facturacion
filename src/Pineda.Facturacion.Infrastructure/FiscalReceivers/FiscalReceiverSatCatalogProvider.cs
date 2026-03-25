using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pineda.Facturacion.Application.Abstractions.FiscalReceivers;

namespace Pineda.Facturacion.Infrastructure.FiscalReceivers;

public sealed class FiscalReceiverSatCatalogProvider : IFiscalReceiverSatCatalogProvider
{
    private const string ResourceName = "Pineda.Facturacion.Infrastructure.Resources.sat_cfdi_40_catalogos_y_compatibilidad_receptor.json";

    private readonly Lazy<CatalogIndex> _catalog;

    public FiscalReceiverSatCatalogProvider()
    {
        _catalog = new Lazy<CatalogIndex>(LoadCatalog, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public FiscalReceiverSatCatalog GetCatalog() => _catalog.Value.Catalog;

    public bool FiscalRegimeExists(string code)
        => _catalog.Value.RegimeByCode.ContainsKey(Normalize(code));

    public bool CfdiUseExists(string code)
        => _catalog.Value.CfdiUseByCode.ContainsKey(Normalize(code));

    public bool IsCfdiUseCompatibleWithRegime(string fiscalRegimeCode, string cfdiUseCode)
    {
        var normalizedRegimeCode = Normalize(fiscalRegimeCode);
        var normalizedCfdiUseCode = Normalize(cfdiUseCode);

        return _catalog.Value.AllowedCfdiUseCodesByRegimeCode.TryGetValue(normalizedRegimeCode, out var allowedCfdiUses)
               && allowedCfdiUses.Contains(normalizedCfdiUseCode);
    }

    private static CatalogIndex LoadCatalog()
    {
        var assembly = typeof(FiscalReceiverSatCatalogProvider).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded SAT catalog resource '{ResourceName}' was not found.");

        var document = JsonSerializer.Deserialize<CatalogDocument>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("SAT receiver catalog resource is empty or invalid.");

        var catalog = new FiscalReceiverSatCatalog
        {
            RegimenFiscal = (document.Catalogs?.RegimenFiscal ?? [])
                .Select(MapOption)
                .ToArray(),
            UsoCfdi = (document.Catalogs?.UsoCfdi ?? [])
                .Select(MapOption)
                .ToArray(),
            ByRegimenFiscal = (document.Compatibility?.ByRegimenFiscal ?? [])
                .Select(regime => new FiscalReceiverSatRegimeCompatibility
                {
                    Code = Normalize(regime.Code),
                    Description = regime.Description?.Trim() ?? string.Empty,
                    AllowedUsoCfdi = (regime.AllowedUsoCfdi ?? [])
                        .Select(MapOption)
                        .ToArray()
                })
                .ToArray()
        };

        return new CatalogIndex
        {
            Catalog = catalog,
            RegimeByCode = catalog.RegimenFiscal.ToDictionary(x => Normalize(x.Code), StringComparer.OrdinalIgnoreCase),
            CfdiUseByCode = catalog.UsoCfdi.ToDictionary(x => Normalize(x.Code), StringComparer.OrdinalIgnoreCase),
            AllowedCfdiUseCodesByRegimeCode = catalog.ByRegimenFiscal.ToDictionary(
                x => Normalize(x.Code),
                x => x.AllowedUsoCfdi.Select(usage => Normalize(usage.Code)).ToHashSet(StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase)
        };
    }

    private static FiscalReceiverSatCatalogOption MapOption(CatalogOption option)
    {
        return new FiscalReceiverSatCatalogOption
        {
            Code = Normalize(option.Code),
            Description = option.Description?.Trim() ?? string.Empty
        };
    }

    private static string Normalize(string? value)
        => value?.Trim().ToUpperInvariant() ?? string.Empty;

    private sealed class CatalogIndex
    {
        public FiscalReceiverSatCatalog Catalog { get; init; } = new();

        public IReadOnlyDictionary<string, FiscalReceiverSatCatalogOption> RegimeByCode { get; init; } = new Dictionary<string, FiscalReceiverSatCatalogOption>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, FiscalReceiverSatCatalogOption> CfdiUseByCode { get; init; } = new Dictionary<string, FiscalReceiverSatCatalogOption>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, HashSet<string>> AllowedCfdiUseCodesByRegimeCode { get; init; } = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class CatalogDocument
    {
        public CatalogSection? Catalogs { get; init; }

        public CompatibilitySection? Compatibility { get; init; }
    }

    private sealed class CatalogSection
    {
        public IReadOnlyList<CatalogOption>? RegimenFiscal { get; init; }

        public IReadOnlyList<CatalogOption>? UsoCfdi { get; init; }
    }

    private sealed class CompatibilitySection
    {
        public IReadOnlyList<RegimeCompatibilityOption>? ByRegimenFiscal { get; init; }
    }

    private class CatalogOption
    {
        public string? Code { get; init; }

        public string? Description { get; init; }
    }

    private sealed class RegimeCompatibilityOption : CatalogOption
    {
        [JsonPropertyName("allowed_uso_cfdi")]
        public IReadOnlyList<CatalogOption>? AllowedUsoCfdi { get; init; }
    }
}
