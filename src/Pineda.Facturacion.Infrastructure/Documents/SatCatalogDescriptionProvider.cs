using Pineda.Facturacion.Application.Abstractions.Documents;
using Pineda.Facturacion.Application.Abstractions.FiscalReceivers;

namespace Pineda.Facturacion.Infrastructure.Documents;

public sealed class SatCatalogDescriptionProvider : ISatCatalogDescriptionProvider
{
    private static readonly IReadOnlyDictionary<string, string> PaymentForms = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["01"] = "Efectivo",
        ["02"] = "Cheque nominativo",
        ["03"] = "Transferencia electronica de fondos",
        ["04"] = "Tarjeta de credito",
        ["05"] = "Monedero electronico",
        ["06"] = "Dinero electronico",
        ["08"] = "Vales de despensa",
        ["12"] = "Dacion en pago",
        ["13"] = "Pago por subrogacion",
        ["14"] = "Pago por consignacion",
        ["15"] = "Condonacion",
        ["17"] = "Compensacion",
        ["23"] = "Novacion",
        ["24"] = "Confusion",
        ["25"] = "Remision de deuda",
        ["26"] = "Prescripcion o caducidad",
        ["27"] = "A satisfaccion del acreedor",
        ["28"] = "Tarjeta de debito",
        ["29"] = "Tarjeta de servicios",
        ["30"] = "Aplicacion de anticipos",
        ["31"] = "Intermediario pagos",
        ["99"] = "Por definir"
    };

    private static readonly IReadOnlyDictionary<string, string> PaymentMethods = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["PUE"] = "Pago en una sola exhibicion",
        ["PPD"] = "Pago en parcialidades o diferido"
    };

    private static readonly IReadOnlyDictionary<string, string> ExportCodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["01"] = "No aplica",
        ["02"] = "Definitiva",
        ["03"] = "Temporal"
    };

    private readonly IFiscalReceiverSatCatalogProvider _receiverSatCatalogProvider;

    public SatCatalogDescriptionProvider(IFiscalReceiverSatCatalogProvider receiverSatCatalogProvider)
    {
        _receiverSatCatalogProvider = receiverSatCatalogProvider;
    }

    public string FormatFiscalRegime(string? code)
        => Format(code, TryGetFiscalRegimeDescription(code));

    public IReadOnlyDictionary<string, string> GetPaymentForms()
        => PaymentForms;

    public IReadOnlyDictionary<string, string> GetPaymentMethods()
        => PaymentMethods;

    public string FormatCfdiUse(string? code)
        => Format(code, TryGetCfdiUseDescription(code));

    public string FormatPaymentForm(string? code)
        => Format(code, Lookup(PaymentForms, code));

    public string FormatPaymentMethod(string? code)
        => Format(code, Lookup(PaymentMethods, code));

    public string FormatExportCode(string? code)
        => Format(code, Lookup(ExportCodes, code));

    private string? TryGetFiscalRegimeDescription(string? code)
    {
        var normalized = Normalize(code);
        if (normalized.Length == 0)
        {
            return null;
        }

        return _receiverSatCatalogProvider.GetCatalog().RegimenFiscal
            .FirstOrDefault(x => string.Equals(x.Code, normalized, StringComparison.OrdinalIgnoreCase))
            ?.Description;
    }

    private string? TryGetCfdiUseDescription(string? code)
    {
        var normalized = Normalize(code);
        if (normalized.Length == 0)
        {
            return null;
        }

        return _receiverSatCatalogProvider.GetCatalog().UsoCfdi
            .FirstOrDefault(x => string.Equals(x.Code, normalized, StringComparison.OrdinalIgnoreCase))
            ?.Description;
    }

    private static string? Lookup(IReadOnlyDictionary<string, string> source, string? code)
    {
        var normalized = Normalize(code);
        return normalized.Length > 0 && source.TryGetValue(normalized, out var description)
            ? description
            : null;
    }

    private static string Format(string? code, string? description)
    {
        var normalized = Normalize(code);
        if (normalized.Length == 0)
        {
            return "N/D";
        }

        return string.IsNullOrWhiteSpace(description)
            ? normalized
            : $"{normalized} - {description}";
    }

    private static string Normalize(string? value) => value?.Trim().ToUpperInvariant() ?? string.Empty;
}
