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
        var error = await ValidateDetailedAsync(
            satProductServiceCode,
            satUnitCode,
            taxObjectCode,
            cancellationToken);

        return error?.ErrorMessage;
    }

    public async Task<ProductFiscalProfileSatCatalogValidationError?> ValidateDetailedAsync(
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
                return ProductFiscalProfileSatCatalogValidationError.ProductService(
                    normalizedProductCode,
                    $"SAT product/service code '{normalizedProductCode}' was not found or is inactive.");
            }
        }

        var normalizedUnitCode = FiscalMasterDataNormalization.NormalizeRequiredCode(satUnitCode);
        if (_satClaveUnidadRepository is not null)
        {
            var unit = await _satClaveUnidadRepository.GetByCodeAsync(normalizedUnitCode, cancellationToken);
            if (unit is null || !unit.IsActive)
            {
                return ProductFiscalProfileSatCatalogValidationError.Unit(
                    normalizedUnitCode,
                    $"SAT unit code '{normalizedUnitCode}' was not found or is inactive.");
            }
        }

        var normalizedTaxObjectCode = FiscalMasterDataNormalization.NormalizeRequiredCode(taxObjectCode);
        if (!SupportedTaxObjectCodes.Contains(normalizedTaxObjectCode))
        {
            return ProductFiscalProfileSatCatalogValidationError.TaxObject(
                normalizedTaxObjectCode,
                $"Tax object code '{normalizedTaxObjectCode}' is not supported.");
        }

        return null;
    }
}

internal sealed record ProductFiscalProfileSatCatalogValidationError(
    string FieldName,
    string DisplayName,
    string InvalidValue,
    string CatalogName,
    string IssueText,
    string ErrorMessage)
{
    public static ProductFiscalProfileSatCatalogValidationError ProductService(
        string invalidValue,
        string errorMessage)
    {
        return new ProductFiscalProfileSatCatalogValidationError(
            "SatProductServiceCode",
            "ClaveProdServ",
            invalidValue,
            "c_ClaveProdServ",
            "inválida o inactiva",
            errorMessage);
    }

    public static ProductFiscalProfileSatCatalogValidationError Unit(
        string invalidValue,
        string errorMessage)
    {
        return new ProductFiscalProfileSatCatalogValidationError(
            "SatUnitCode",
            "ClaveUnidad",
            invalidValue,
            "c_ClaveUnidad",
            "inválida o inactiva",
            errorMessage);
    }

    public static ProductFiscalProfileSatCatalogValidationError TaxObject(
        string invalidValue,
        string errorMessage)
    {
        return new ProductFiscalProfileSatCatalogValidationError(
            "TaxObjectCode",
            "ObjetoImp",
            invalidValue,
            "c_ObjetoImp",
            "inválido o no soportado",
            errorMessage);
    }
}
