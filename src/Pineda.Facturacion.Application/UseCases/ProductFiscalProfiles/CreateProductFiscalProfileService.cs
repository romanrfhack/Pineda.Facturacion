using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.ProductFiscalProfiles;

public class CreateProductFiscalProfileService
{
    private readonly IProductFiscalProfileRepository _productFiscalProfileRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ProductFiscalProfileSatCatalogValidation _satCatalogValidation;

    public CreateProductFiscalProfileService(IProductFiscalProfileRepository productFiscalProfileRepository, IUnitOfWork unitOfWork)
        : this(productFiscalProfileRepository, unitOfWork, new ProductFiscalProfileSatCatalogValidation())
    {
    }

    public CreateProductFiscalProfileService(
        IProductFiscalProfileRepository productFiscalProfileRepository,
        IUnitOfWork unitOfWork,
        ISatProductServiceCatalogRepository satProductServiceCatalogRepository,
        ISatClaveUnidadRepository satClaveUnidadRepository)
        : this(
            productFiscalProfileRepository,
            unitOfWork,
            new ProductFiscalProfileSatCatalogValidation(satProductServiceCatalogRepository, satClaveUnidadRepository))
    {
    }

    private CreateProductFiscalProfileService(
        IProductFiscalProfileRepository productFiscalProfileRepository,
        IUnitOfWork unitOfWork,
        ProductFiscalProfileSatCatalogValidation satCatalogValidation)
    {
        _productFiscalProfileRepository = productFiscalProfileRepository;
        _unitOfWork = unitOfWork;
        _satCatalogValidation = satCatalogValidation;
    }

    public async Task<CreateProductFiscalProfileResult> ExecuteAsync(CreateProductFiscalProfileCommand command, CancellationToken cancellationToken = default)
    {
        var validationError = Validate(command);
        if (validationError is not null)
        {
            return new CreateProductFiscalProfileResult
            {
                Outcome = CreateProductFiscalProfileOutcome.ValidationFailed,
                IsSuccess = false,
                ErrorMessage = validationError
            };
        }

        var satCatalogValidationError = await _satCatalogValidation.ValidateAsync(
            command.SatProductServiceCode,
            command.SatUnitCode,
            command.TaxObjectCode,
            cancellationToken);
        if (satCatalogValidationError is not null)
        {
            return new CreateProductFiscalProfileResult
            {
                Outcome = CreateProductFiscalProfileOutcome.ValidationFailed,
                IsSuccess = false,
                ErrorMessage = satCatalogValidationError
            };
        }

        var normalizedInternalCode = FiscalMasterDataNormalization.NormalizeRequiredCode(command.InternalCode);
        var existing = await _productFiscalProfileRepository.GetByInternalCodeAsync(normalizedInternalCode, cancellationToken);
        if (existing is not null)
        {
            return new CreateProductFiscalProfileResult
            {
                Outcome = CreateProductFiscalProfileOutcome.Conflict,
                IsSuccess = false,
                ErrorMessage = $"A product fiscal profile with internal code '{normalizedInternalCode}' already exists."
            };
        }

        var now = DateTime.UtcNow;
        var normalizedDescription = FiscalMasterDataNormalization.NormalizeRequiredText(command.Description);
        var productFiscalProfile = new ProductFiscalProfile
        {
            InternalCode = normalizedInternalCode,
            Description = normalizedDescription,
            NormalizedDescription = FiscalMasterDataNormalization.NormalizeSearchableText(normalizedDescription),
            SatProductServiceCode = FiscalMasterDataNormalization.NormalizeRequiredCode(command.SatProductServiceCode),
            SatUnitCode = FiscalMasterDataNormalization.NormalizeRequiredCode(command.SatUnitCode),
            TaxObjectCode = FiscalMasterDataNormalization.NormalizeRequiredCode(command.TaxObjectCode),
            VatRate = command.VatRate,
            DefaultUnitText = FiscalMasterDataNormalization.NormalizeOptionalText(command.DefaultUnitText),
            IsActive = command.IsActive,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _productFiscalProfileRepository.AddAsync(productFiscalProfile, cancellationToken);
        await _productFiscalProfileRepository.EnsureEffectiveAssignmentAsync(
            productFiscalProfile,
            ProductFiscalAssignmentConventions.ManualSource,
            ProductFiscalAssignmentConventions.ManualConfidence,
            ProductFiscalAssignmentConventions.BootstrapReviewStatus,
            null,
            now,
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateProductFiscalProfileResult
        {
            Outcome = CreateProductFiscalProfileOutcome.Created,
            IsSuccess = true,
            ProductFiscalProfileId = productFiscalProfile.Id
        };
    }

    private static string? Validate(CreateProductFiscalProfileCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.InternalCode)) return "Internal code is required.";
        if (string.IsNullOrWhiteSpace(command.Description)) return "Description is required.";
        if (string.IsNullOrWhiteSpace(command.SatProductServiceCode)) return "SAT product/service code is required.";
        if (string.IsNullOrWhiteSpace(command.SatUnitCode)) return "SAT unit code is required.";
        if (string.IsNullOrWhiteSpace(command.TaxObjectCode)) return "Tax object code is required.";
        if (command.VatRate < 0) return "VAT rate must be zero or greater.";
        return null;
    }
}
