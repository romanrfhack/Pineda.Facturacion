using Pineda.Facturacion.Application.Abstractions.FiscalReceivers;
using Pineda.Facturacion.Application.Common;

namespace Pineda.Facturacion.Application.UseCases.FiscalReceivers;

internal static class FiscalReceiverSatCatalogValidation
{
    public static string? ValidateCodes(
        string fiscalRegimeCode,
        string cfdiUseCode,
        IFiscalReceiverSatCatalogProvider catalogProvider)
    {
        var normalizedFiscalRegimeCode = FiscalMasterDataNormalization.NormalizeRequiredCode(fiscalRegimeCode);
        var normalizedCfdiUseCode = FiscalMasterDataNormalization.NormalizeRequiredCode(cfdiUseCode);

        if (!catalogProvider.FiscalRegimeExists(normalizedFiscalRegimeCode))
        {
            return $"Fiscal regime code '{normalizedFiscalRegimeCode}' is not valid for SAT CFDI 4.0.";
        }

        if (!catalogProvider.CfdiUseExists(normalizedCfdiUseCode))
        {
            return $"Default CFDI use code '{normalizedCfdiUseCode}' is not valid for SAT CFDI 4.0.";
        }

        if (!catalogProvider.IsCfdiUseCompatibleWithRegime(normalizedFiscalRegimeCode, normalizedCfdiUseCode))
        {
            return $"Default CFDI use code '{normalizedCfdiUseCode}' is not compatible with fiscal regime '{normalizedFiscalRegimeCode}'.";
        }

        return null;
    }
}
