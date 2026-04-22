using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;

namespace Pineda.Facturacion.Application.UseCases.ProductFiscalProfiles;

internal sealed class ProductFiscalProfileSatCatalogValidation
{
    private static readonly HashSet<string> SupportedTaxObjectCodes = ["01", "02", "03", "04"];

    private readonly ISatProductServiceCatalogRepository? _satProductServiceCatalogRepository;
    private readonly ISatClaveUnidadRepository? _satClaveUnidadRepository;

    public ProductFiscalProfileSatCatalogValidation()
    {
    }

    public ProductFiscalProfileSatCatalogValidation(
        ISatProductServiceCatalogRepository satProductServiceCatalogRepository,
        ISatClaveUnidadRepository satClaveUnidadRepository)
    {
        _satProductServiceCatalogRepository = satProductServiceCatalogRepository;
        _satClaveUnidadRepository = satClaveUnidadRepository;
    }

    public async Task<string?> ValidateAsync(
        string satProductServiceCode,
        string satUnitCode,
        string taxObjectCode,
        CancellationToken cancellationToken)
    {
        var normalizedProductCode = FiscalMasterDataNormalization.NormalizeRequiredCode(satProductServiceCode);
        if (_satProductServiceCatalogRepository is not null)
        {
            var product = await _satProductServiceCatalogRepository.GetByCodeAsync(normalizedProductCode, cancellationToken);
            if (product is null || !product.IsActive)
            {
                return $"SAT product/service code '{normalizedProductCode}' was not found or is inactive.";
            }
        }

        var normalizedUnitCode = FiscalMasterDataNormalization.NormalizeRequiredCode(satUnitCode);
        if (_satClaveUnidadRepository is not null)
        {
            var unit = await _satClaveUnidadRepository.GetByCodeAsync(normalizedUnitCode, cancellationToken);
            if (unit is null || !unit.IsActive)
            {
                return $"SAT unit code '{normalizedUnitCode}' was not found or is inactive.";
            }
        }

        var normalizedTaxObjectCode = FiscalMasterDataNormalization.NormalizeRequiredCode(taxObjectCode);
        if (!SupportedTaxObjectCodes.Contains(normalizedTaxObjectCode))
        {
            return $"Tax object code '{normalizedTaxObjectCode}' is not supported.";
        }

        return null;
    }
}
