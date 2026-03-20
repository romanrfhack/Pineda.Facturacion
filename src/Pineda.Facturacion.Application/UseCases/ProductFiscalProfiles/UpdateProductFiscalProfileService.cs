using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;

namespace Pineda.Facturacion.Application.UseCases.ProductFiscalProfiles;

public class UpdateProductFiscalProfileService
{
    private readonly IProductFiscalProfileRepository _productFiscalProfileRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateProductFiscalProfileService(IProductFiscalProfileRepository productFiscalProfileRepository, IUnitOfWork unitOfWork)
    {
        _productFiscalProfileRepository = productFiscalProfileRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<UpdateProductFiscalProfileResult> ExecuteAsync(UpdateProductFiscalProfileCommand command, CancellationToken cancellationToken = default)
    {
        if (command.Id <= 0)
        {
            return ValidationFailure("Product fiscal profile id is required.");
        }

        var validationError = Validate(command);
        if (validationError is not null)
        {
            return ValidationFailure(validationError);
        }

        var productFiscalProfile = await _productFiscalProfileRepository.GetByIdAsync(command.Id, cancellationToken);
        if (productFiscalProfile is null)
        {
            return new UpdateProductFiscalProfileResult
            {
                Outcome = UpdateProductFiscalProfileOutcome.NotFound,
                IsSuccess = false,
                ErrorMessage = $"Product fiscal profile '{command.Id}' was not found."
            };
        }

        var normalizedInternalCode = FiscalMasterDataNormalization.NormalizeRequiredCode(command.InternalCode);
        var existing = await _productFiscalProfileRepository.GetByInternalCodeAsync(normalizedInternalCode, cancellationToken);
        if (existing is not null && existing.Id != command.Id)
        {
            return new UpdateProductFiscalProfileResult
            {
                Outcome = UpdateProductFiscalProfileOutcome.Conflict,
                IsSuccess = false,
                ErrorMessage = $"A product fiscal profile with internal code '{normalizedInternalCode}' already exists."
            };
        }

        var normalizedDescription = FiscalMasterDataNormalization.NormalizeRequiredText(command.Description);

        productFiscalProfile.InternalCode = normalizedInternalCode;
        productFiscalProfile.Description = normalizedDescription;
        productFiscalProfile.NormalizedDescription = FiscalMasterDataNormalization.NormalizeSearchableText(normalizedDescription);
        productFiscalProfile.SatProductServiceCode = FiscalMasterDataNormalization.NormalizeRequiredCode(command.SatProductServiceCode);
        productFiscalProfile.SatUnitCode = FiscalMasterDataNormalization.NormalizeRequiredCode(command.SatUnitCode);
        productFiscalProfile.TaxObjectCode = FiscalMasterDataNormalization.NormalizeRequiredCode(command.TaxObjectCode);
        productFiscalProfile.VatRate = command.VatRate;
        productFiscalProfile.DefaultUnitText = FiscalMasterDataNormalization.NormalizeOptionalText(command.DefaultUnitText);
        productFiscalProfile.IsActive = command.IsActive;
        productFiscalProfile.UpdatedAtUtc = DateTime.UtcNow;

        await _productFiscalProfileRepository.UpdateAsync(productFiscalProfile, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new UpdateProductFiscalProfileResult
        {
            Outcome = UpdateProductFiscalProfileOutcome.Updated,
            IsSuccess = true,
            ProductFiscalProfileId = productFiscalProfile.Id
        };
    }

    private static UpdateProductFiscalProfileResult ValidationFailure(string errorMessage)
    {
        return new UpdateProductFiscalProfileResult
        {
            Outcome = UpdateProductFiscalProfileOutcome.ValidationFailed,
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }

    private static string? Validate(UpdateProductFiscalProfileCommand command)
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
