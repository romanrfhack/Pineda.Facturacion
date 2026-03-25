namespace Pineda.Facturacion.Application.Abstractions.FiscalReceivers;

public interface IFiscalReceiverSatCatalogProvider
{
    FiscalReceiverSatCatalog GetCatalog();

    bool FiscalRegimeExists(string code);

    bool CfdiUseExists(string code);

    bool IsCfdiUseCompatibleWithRegime(string fiscalRegimeCode, string cfdiUseCode);
}

public sealed class FiscalReceiverSatCatalog
{
    public IReadOnlyList<FiscalReceiverSatCatalogOption> RegimenFiscal { get; init; } = [];

    public IReadOnlyList<FiscalReceiverSatCatalogOption> UsoCfdi { get; init; } = [];

    public IReadOnlyList<FiscalReceiverSatRegimeCompatibility> ByRegimenFiscal { get; init; } = [];
}

public class FiscalReceiverSatCatalogOption
{
    public string Code { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;
}

public sealed class FiscalReceiverSatRegimeCompatibility : FiscalReceiverSatCatalogOption
{
    public IReadOnlyList<FiscalReceiverSatCatalogOption> AllowedUsoCfdi { get; init; } = [];
}
