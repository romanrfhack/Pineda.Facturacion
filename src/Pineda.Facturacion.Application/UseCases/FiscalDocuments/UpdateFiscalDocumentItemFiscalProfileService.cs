using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Application.UseCases.ProductFiscalProfiles;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public sealed class UpdateFiscalDocumentItemFiscalProfileService
{
    private readonly IFiscalDocumentRepository _fiscalDocumentRepository;
    private readonly IBillingDocumentRepository _billingDocumentRepository;
    private readonly IFiscalStampRepository _fiscalStampRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ProductFiscalProfileSatCatalogValidation _satCatalogValidation;

    public UpdateFiscalDocumentItemFiscalProfileService(
        IFiscalDocumentRepository fiscalDocumentRepository,
        IBillingDocumentRepository billingDocumentRepository,
        IFiscalStampRepository fiscalStampRepository,
        IUnitOfWork unitOfWork)
        : this(
            fiscalDocumentRepository,
            billingDocumentRepository,
            fiscalStampRepository,
            unitOfWork,
            new ProductFiscalProfileSatCatalogValidation())
    {
    }

    public UpdateFiscalDocumentItemFiscalProfileService(
        IFiscalDocumentRepository fiscalDocumentRepository,
        IBillingDocumentRepository billingDocumentRepository,
        IFiscalStampRepository fiscalStampRepository,
        IUnitOfWork unitOfWork,
        ISatProductServiceCatalogRepository satProductServiceCatalogRepository,
        ISatClaveUnidadRepository satClaveUnidadRepository)
        : this(
            fiscalDocumentRepository,
            billingDocumentRepository,
            fiscalStampRepository,
            unitOfWork,
            new ProductFiscalProfileSatCatalogValidation(
                satProductServiceCatalogRepository,
                satClaveUnidadRepository))
    {
    }

    private UpdateFiscalDocumentItemFiscalProfileService(
        IFiscalDocumentRepository fiscalDocumentRepository,
        IBillingDocumentRepository billingDocumentRepository,
        IFiscalStampRepository fiscalStampRepository,
        IUnitOfWork unitOfWork,
        ProductFiscalProfileSatCatalogValidation satCatalogValidation)
    {
        _fiscalDocumentRepository = fiscalDocumentRepository;
        _billingDocumentRepository = billingDocumentRepository;
        _fiscalStampRepository = fiscalStampRepository;
        _unitOfWork = unitOfWork;
        _satCatalogValidation = satCatalogValidation;
    }

    public async Task<UpdateFiscalDocumentItemFiscalProfileResult> ExecuteAsync(
        UpdateFiscalDocumentItemFiscalProfileCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.FiscalDocumentItemId <= 0)
        {
            return ValidationFailure(
                command.FiscalDocumentItemId,
                0,
                null,
                "Fiscal document item id is required.");
        }

        var fiscalDocument = await _fiscalDocumentRepository.GetTrackedByItemIdAsync(
            command.FiscalDocumentItemId,
            cancellationToken);
        if (fiscalDocument is null)
        {
            return new UpdateFiscalDocumentItemFiscalProfileResult
            {
                Outcome = UpdateFiscalDocumentItemFiscalProfileOutcome.NotFound,
                IsSuccess = false,
                FiscalDocumentItemId = command.FiscalDocumentItemId,
                ErrorMessage = $"Fiscal document item '{command.FiscalDocumentItemId}' was not found."
            };
        }

        var fiscalDocumentItem = fiscalDocument.Items.SingleOrDefault(x => x.Id == command.FiscalDocumentItemId);
        if (fiscalDocumentItem is null)
        {
            return new UpdateFiscalDocumentItemFiscalProfileResult
            {
                Outcome = UpdateFiscalDocumentItemFiscalProfileOutcome.NotFound,
                IsSuccess = false,
                FiscalDocumentId = fiscalDocument.Id,
                FiscalDocumentStatus = fiscalDocument.Status,
                FiscalDocumentItemId = command.FiscalDocumentItemId,
                ErrorMessage = $"Fiscal document item '{command.FiscalDocumentItemId}' was not found."
            };
        }

        var fiscalStamp = await _fiscalStampRepository.GetByFiscalDocumentIdAsync(fiscalDocument.Id, cancellationToken);
        if (fiscalStamp is not null && !string.IsNullOrWhiteSpace(fiscalStamp.Uuid))
        {
            return Conflict(
                fiscalDocument,
                fiscalDocumentItem.Id,
                "Stamped fiscal documents with UUID evidence cannot be edited.");
        }

        if (!FiscalDocumentCompositionEditPolicy.CanEdit(fiscalDocument))
        {
            return Conflict(
                fiscalDocument,
                fiscalDocumentItem.Id,
                $"Fiscal document status '{fiscalDocument.Status}' is not eligible for fiscal line overrides.");
        }

        var validationError = Validate(command);
        if (validationError is not null)
        {
            return ValidationFailure(
                command.FiscalDocumentItemId,
                fiscalDocument.Id,
                fiscalDocument.Status,
                validationError);
        }

        var normalizedTaxObjectCode = FiscalMasterDataNormalization.NormalizeRequiredCode(command.TaxObjectCode);
        var taxCompatibilityError = ValidateTaxObjectCompatibility(normalizedTaxObjectCode, fiscalDocumentItem);
        if (taxCompatibilityError is not null)
        {
            return ValidationFailure(
                fiscalDocumentItem.Id,
                fiscalDocument.Id,
                fiscalDocument.Status,
                taxCompatibilityError);
        }

        var satCatalogValidationError = await _satCatalogValidation.ValidateAsync(
            command.SatProductServiceCode,
            command.SatUnitCode,
            normalizedTaxObjectCode,
            cancellationToken);
        if (satCatalogValidationError is not null)
        {
            return ValidationFailure(
                fiscalDocumentItem.Id,
                fiscalDocument.Id,
                fiscalDocument.Status,
                satCatalogValidationError);
        }

        var billingDocument = await _billingDocumentRepository.GetByIdAsync(
            fiscalDocument.BillingDocumentId,
            cancellationToken);
        var vatRateValidationError = ValidateVatRate(
            command.VatRate,
            fiscalDocument.BillingDocumentId,
            fiscalDocumentItem,
            billingDocument);
        if (vatRateValidationError is not null)
        {
            return ValidationFailure(
                fiscalDocumentItem.Id,
                fiscalDocument.Id,
                fiscalDocument.Status,
                vatRateValidationError);
        }

        fiscalDocumentItem.SatProductServiceCode = FiscalMasterDataNormalization.NormalizeRequiredCode(command.SatProductServiceCode);
        fiscalDocumentItem.SatUnitCode = FiscalMasterDataNormalization.NormalizeRequiredCode(command.SatUnitCode);
        fiscalDocumentItem.TaxObjectCode = normalizedTaxObjectCode;
        fiscalDocumentItem.VatRate = NormalizeVatRate(command.VatRate);
        fiscalDocumentItem.UnitText = FiscalMasterDataNormalization.NormalizeOptionalText(command.UnitText);
        fiscalDocument.UpdatedAtUtc = DateTime.UtcNow;

        var consistencyError = FiscalDocumentSnapshotConsistencyValidator.Validate(fiscalDocument);
        if (consistencyError is not null)
        {
            return ValidationFailure(
                fiscalDocumentItem.Id,
                fiscalDocument.Id,
                fiscalDocument.Status,
                consistencyError);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new UpdateFiscalDocumentItemFiscalProfileResult
        {
            Outcome = UpdateFiscalDocumentItemFiscalProfileOutcome.Updated,
            IsSuccess = true,
            FiscalDocumentId = fiscalDocument.Id,
            FiscalDocumentItemId = fiscalDocumentItem.Id,
            FiscalDocumentStatus = fiscalDocument.Status,
            FiscalDocumentItem = fiscalDocumentItem
        };
    }

    private static string? Validate(UpdateFiscalDocumentItemFiscalProfileCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.SatProductServiceCode))
        {
            return "SAT product/service code is required.";
        }

        if (string.IsNullOrWhiteSpace(command.SatUnitCode))
        {
            return "SAT unit code is required.";
        }

        if (string.IsNullOrWhiteSpace(command.TaxObjectCode))
        {
            return "Tax object code is required.";
        }

        if (command.VatRate < 0m)
        {
            return "VAT rate must be zero or greater.";
        }

        var normalizedUnitText = FiscalMasterDataNormalization.NormalizeOptionalText(command.UnitText);
        if (normalizedUnitText is not null && normalizedUnitText.Length > 100)
        {
            return "Unit text cannot exceed 100 characters.";
        }

        return null;
    }

    private static string? ValidateTaxObjectCompatibility(
        string normalizedTaxObjectCode,
        FiscalDocumentItem fiscalDocumentItem)
    {
        if (string.Equals(normalizedTaxObjectCode, "02", StringComparison.Ordinal))
        {
            return null;
        }

        return fiscalDocumentItem.TaxTotal > 0m
            ? $"Tax object code '{normalizedTaxObjectCode}' is not compatible with fiscal document item line '{fiscalDocumentItem.LineNumber}' because the persisted snapshot already contains tax total '{fiscalDocumentItem.TaxTotal:0.######}'."
            : null;
    }

    private static string? ValidateVatRate(
        decimal requestedVatRate,
        long billingDocumentId,
        FiscalDocumentItem fiscalDocumentItem,
        BillingDocument? billingDocument)
    {
        var normalizedRequestedVatRate = NormalizeVatRate(requestedVatRate);
        if (normalizedRequestedVatRate == NormalizeVatRate(fiscalDocumentItem.VatRate))
        {
            return null;
        }

        if (billingDocument is null)
        {
            return $"Billing document '{billingDocumentId}' was not found while validating VAT rate override.";
        }

        var billingDocumentItem = ResolveBillingDocumentItem(billingDocument, fiscalDocumentItem);
        if (billingDocumentItem is null)
        {
            return $"Fiscal document item line '{fiscalDocumentItem.LineNumber}' is no longer linked to a billable line, so VAT rate cannot be overridden safely.";
        }

        var normalizedBillingVatRate = NormalizeVatRate(billingDocumentItem.TaxRate);
        return normalizedRequestedVatRate == normalizedBillingVatRate
            ? null
            : $"VAT rate override '{normalizedRequestedVatRate:0.######}' must match current billing item tax rate '{normalizedBillingVatRate:0.######}'.";
    }

    private static BillingDocumentItem? ResolveBillingDocumentItem(
        BillingDocument billingDocument,
        FiscalDocumentItem fiscalDocumentItem)
    {
        if (fiscalDocumentItem.BillingDocumentItemId.HasValue)
        {
            var exact = billingDocument.Items.SingleOrDefault(
                x => x.Id == fiscalDocumentItem.BillingDocumentItemId.Value);
            if (exact is not null)
            {
                return exact;
            }
        }

        return billingDocument.Items.SingleOrDefault(x => x.LineNumber == fiscalDocumentItem.LineNumber);
    }

    private static decimal NormalizeVatRate(decimal vatRate)
    {
        return decimal.Round(vatRate, 6, MidpointRounding.AwayFromZero);
    }

    private static UpdateFiscalDocumentItemFiscalProfileResult Conflict(
        FiscalDocument fiscalDocument,
        long fiscalDocumentItemId,
        string errorMessage)
    {
        return new UpdateFiscalDocumentItemFiscalProfileResult
        {
            Outcome = UpdateFiscalDocumentItemFiscalProfileOutcome.Conflict,
            IsSuccess = false,
            FiscalDocumentId = fiscalDocument.Id,
            FiscalDocumentItemId = fiscalDocumentItemId,
            FiscalDocumentStatus = fiscalDocument.Status,
            ErrorMessage = errorMessage
        };
    }

    private static UpdateFiscalDocumentItemFiscalProfileResult ValidationFailure(
        long fiscalDocumentItemId,
        long fiscalDocumentId,
        Pineda.Facturacion.Domain.Enums.FiscalDocumentStatus? fiscalDocumentStatus,
        string errorMessage)
    {
        return new UpdateFiscalDocumentItemFiscalProfileResult
        {
            Outcome = UpdateFiscalDocumentItemFiscalProfileOutcome.ValidationFailed,
            IsSuccess = false,
            FiscalDocumentId = fiscalDocumentId,
            FiscalDocumentItemId = fiscalDocumentItemId,
            FiscalDocumentStatus = fiscalDocumentStatus,
            ErrorMessage = errorMessage
        };
    }
}
