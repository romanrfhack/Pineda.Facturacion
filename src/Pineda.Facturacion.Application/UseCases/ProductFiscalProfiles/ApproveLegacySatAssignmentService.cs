using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;

namespace Pineda.Facturacion.Application.UseCases.ProductFiscalProfiles;

public sealed class ApproveLegacySatAssignmentService
{
    private const string DefaultTaxObjectCode = "02";
    private const decimal DefaultVatRate = 0.160000m;

    private readonly IProductFiscalProfileRepository _productFiscalProfileRepository;
    private readonly ISatProductServiceCatalogRepository _satProductServiceCatalogRepository;
    private readonly ISatClaveUnidadRepository _satClaveUnidadRepository;
    private readonly CreateProductFiscalProfileService _createProductFiscalProfileService;
    private readonly UpdateProductFiscalProfileService _updateProductFiscalProfileService;

    public ApproveLegacySatAssignmentService(
        IProductFiscalProfileRepository productFiscalProfileRepository,
        ISatProductServiceCatalogRepository satProductServiceCatalogRepository,
        ISatClaveUnidadRepository satClaveUnidadRepository,
        CreateProductFiscalProfileService createProductFiscalProfileService,
        UpdateProductFiscalProfileService updateProductFiscalProfileService)
    {
        _productFiscalProfileRepository = productFiscalProfileRepository;
        _satProductServiceCatalogRepository = satProductServiceCatalogRepository;
        _satClaveUnidadRepository = satClaveUnidadRepository;
        _createProductFiscalProfileService = createProductFiscalProfileService;
        _updateProductFiscalProfileService = updateProductFiscalProfileService;
    }

    public async Task<ApproveLegacySatAssignmentResult> ExecuteAsync(
        ApproveLegacySatAssignmentCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.InternalCode))
        {
            return ValidationFailure("Internal code is required.");
        }

        if (string.IsNullOrWhiteSpace(command.Description))
        {
            return ValidationFailure("Description is required.");
        }

        if (string.IsNullOrWhiteSpace(command.SatProductServiceCode))
        {
            return ValidationFailure("SAT product/service code is required.");
        }

        if (string.IsNullOrWhiteSpace(command.SatUnitCode))
        {
            return ValidationFailure("SAT unit code is required.");
        }

        var satProductServiceCode = FiscalMasterDataNormalization.NormalizeRequiredCode(command.SatProductServiceCode);
        var satUnitCode = FiscalMasterDataNormalization.NormalizeRequiredCode(command.SatUnitCode);

        var productCatalogEntry = await _satProductServiceCatalogRepository.GetByCodeAsync(satProductServiceCode, cancellationToken);
        if (productCatalogEntry is null || !productCatalogEntry.IsActive)
        {
            return ValidationFailure($"SAT product/service code '{satProductServiceCode}' was not found or is inactive.");
        }

        var unitCatalogEntry = await _satClaveUnidadRepository.GetByCodeAsync(satUnitCode, cancellationToken);
        if (unitCatalogEntry is null || !unitCatalogEntry.IsActive)
        {
            return ValidationFailure($"SAT unit code '{satUnitCode}' was not found or is inactive.");
        }

        var normalizedInternalCode = FiscalMasterDataNormalization.NormalizeRequiredCode(command.InternalCode);
        var existingProfile = await _productFiscalProfileRepository.GetByInternalCodeAsync(normalizedInternalCode, cancellationToken);

        if (existingProfile is null)
        {
            var effectiveAssignment = await _productFiscalProfileRepository.GetEffectiveAssignmentAsync(
                normalizedInternalCode,
                DateTime.UtcNow,
                cancellationToken);

            var createResult = await _createProductFiscalProfileService.ExecuteAsync(new CreateProductFiscalProfileCommand
            {
                InternalCode = normalizedInternalCode,
                Description = command.Description,
                SatProductServiceCode = satProductServiceCode,
                SatUnitCode = satUnitCode,
                TaxObjectCode = effectiveAssignment?.TaxObjectCode ?? DefaultTaxObjectCode,
                VatRate = effectiveAssignment?.VatRate ?? DefaultVatRate,
                DefaultUnitText = ResolveDefaultUnitText(command.DefaultUnitText, unitCatalogEntry.Description, effectiveAssignment?.DefaultUnitText),
                IsActive = true
            }, cancellationToken);

            return new ApproveLegacySatAssignmentResult
            {
                Outcome = createResult.Outcome switch
                {
                    CreateProductFiscalProfileOutcome.Created => ApproveLegacySatAssignmentOutcome.Created,
                    CreateProductFiscalProfileOutcome.Conflict => ApproveLegacySatAssignmentOutcome.Conflict,
                    CreateProductFiscalProfileOutcome.ValidationFailed => ApproveLegacySatAssignmentOutcome.ValidationFailed,
                    _ => ApproveLegacySatAssignmentOutcome.Failed
                },
                IsSuccess = createResult.IsSuccess,
                ErrorMessage = createResult.ErrorMessage,
                ProductFiscalProfileId = createResult.ProductFiscalProfileId
            };
        }

        var updateResult = await _updateProductFiscalProfileService.ExecuteAsync(new UpdateProductFiscalProfileCommand
        {
            Id = existingProfile.Id,
            InternalCode = normalizedInternalCode,
            Description = command.Description,
            SatProductServiceCode = satProductServiceCode,
            SatUnitCode = satUnitCode,
            TaxObjectCode = existingProfile.TaxObjectCode,
            VatRate = existingProfile.VatRate,
            DefaultUnitText = ResolveDefaultUnitText(command.DefaultUnitText, unitCatalogEntry.Description, existingProfile.DefaultUnitText),
            IsActive = true
        }, cancellationToken);

        return new ApproveLegacySatAssignmentResult
        {
            Outcome = updateResult.Outcome switch
            {
                UpdateProductFiscalProfileOutcome.Updated => ApproveLegacySatAssignmentOutcome.Updated,
                UpdateProductFiscalProfileOutcome.Conflict => ApproveLegacySatAssignmentOutcome.Conflict,
                UpdateProductFiscalProfileOutcome.ValidationFailed => ApproveLegacySatAssignmentOutcome.ValidationFailed,
                _ => ApproveLegacySatAssignmentOutcome.Failed
            },
            IsSuccess = updateResult.IsSuccess,
            ErrorMessage = updateResult.ErrorMessage,
            ProductFiscalProfileId = updateResult.ProductFiscalProfileId
        };
    }

    private static string? ResolveDefaultUnitText(string? requestedValue, string? catalogDescription, string? currentValue)
    {
        return FiscalMasterDataNormalization.NormalizeOptionalText(requestedValue)
            ?? FiscalMasterDataNormalization.NormalizeOptionalText(currentValue)
            ?? FiscalMasterDataNormalization.NormalizeOptionalText(catalogDescription);
    }

    private static ApproveLegacySatAssignmentResult ValidationFailure(string errorMessage)
    {
        return new ApproveLegacySatAssignmentResult
        {
            Outcome = ApproveLegacySatAssignmentOutcome.ValidationFailed,
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }
}
