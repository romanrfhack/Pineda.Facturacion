using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;

namespace Pineda.Facturacion.Application.UseCases.FiscalReceivers;

public class UpdateFiscalReceiverService
{
    private readonly IFiscalReceiverRepository _fiscalReceiverRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateFiscalReceiverService(IFiscalReceiverRepository fiscalReceiverRepository, IUnitOfWork unitOfWork)
    {
        _fiscalReceiverRepository = fiscalReceiverRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<UpdateFiscalReceiverResult> ExecuteAsync(UpdateFiscalReceiverCommand command, CancellationToken cancellationToken = default)
    {
        if (command.Id <= 0)
        {
            return ValidationFailure("Fiscal receiver id is required.");
        }

        var validationError = Validate(command);
        if (validationError is not null)
        {
            return ValidationFailure(validationError);
        }

        var fiscalReceiver = await _fiscalReceiverRepository.GetByIdAsync(command.Id, cancellationToken);
        if (fiscalReceiver is null)
        {
            return new UpdateFiscalReceiverResult
            {
                Outcome = UpdateFiscalReceiverOutcome.NotFound,
                IsSuccess = false,
                ErrorMessage = $"Fiscal receiver '{command.Id}' was not found."
            };
        }

        var normalizedRfc = FiscalMasterDataNormalization.NormalizeRfc(command.Rfc);
        var existing = await _fiscalReceiverRepository.GetByRfcAsync(normalizedRfc, cancellationToken);
        if (existing is not null && existing.Id != command.Id)
        {
            return new UpdateFiscalReceiverResult
            {
                Outcome = UpdateFiscalReceiverOutcome.Conflict,
                IsSuccess = false,
                ErrorMessage = $"A fiscal receiver with RFC '{normalizedRfc}' already exists."
            };
        }

        var normalizedLegalName = FiscalMasterDataNormalization.NormalizeRequiredText(command.LegalName);
        var normalizedSearchAlias = FiscalMasterDataNormalization.NormalizeOptionalText(command.SearchAlias);

        fiscalReceiver.Rfc = normalizedRfc;
        fiscalReceiver.LegalName = normalizedLegalName;
        fiscalReceiver.NormalizedLegalName = FiscalMasterDataNormalization.NormalizeSearchableText(normalizedLegalName);
        fiscalReceiver.FiscalRegimeCode = FiscalMasterDataNormalization.NormalizeRequiredCode(command.FiscalRegimeCode);
        fiscalReceiver.CfdiUseCodeDefault = FiscalMasterDataNormalization.NormalizeRequiredCode(command.CfdiUseCodeDefault);
        fiscalReceiver.PostalCode = FiscalMasterDataNormalization.NormalizeRequiredCode(command.PostalCode);
        fiscalReceiver.CountryCode = FiscalMasterDataNormalization.NormalizeOptionalText(command.CountryCode)?.ToUpperInvariant();
        fiscalReceiver.ForeignTaxRegistration = FiscalMasterDataNormalization.NormalizeOptionalText(command.ForeignTaxRegistration);
        fiscalReceiver.Email = FiscalMasterDataNormalization.NormalizeOptionalText(command.Email);
        fiscalReceiver.Phone = FiscalMasterDataNormalization.NormalizeOptionalText(command.Phone);
        fiscalReceiver.SearchAlias = normalizedSearchAlias;
        fiscalReceiver.NormalizedSearchAlias = normalizedSearchAlias is null
            ? null
            : FiscalMasterDataNormalization.NormalizeSearchableText(normalizedSearchAlias);
        fiscalReceiver.IsActive = command.IsActive;
        fiscalReceiver.UpdatedAtUtc = DateTime.UtcNow;

        await _fiscalReceiverRepository.UpdateAsync(fiscalReceiver, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new UpdateFiscalReceiverResult
        {
            Outcome = UpdateFiscalReceiverOutcome.Updated,
            IsSuccess = true,
            FiscalReceiverId = fiscalReceiver.Id
        };
    }

    private static UpdateFiscalReceiverResult ValidationFailure(string errorMessage)
    {
        return new UpdateFiscalReceiverResult
        {
            Outcome = UpdateFiscalReceiverOutcome.ValidationFailed,
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }

    private static string? Validate(UpdateFiscalReceiverCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Rfc)) return "RFC is required.";
        if (string.IsNullOrWhiteSpace(command.LegalName)) return "Legal name is required.";
        if (string.IsNullOrWhiteSpace(command.FiscalRegimeCode)) return "Fiscal regime code is required.";
        if (string.IsNullOrWhiteSpace(command.CfdiUseCodeDefault)) return "Default CFDI use code is required.";
        if (string.IsNullOrWhiteSpace(command.PostalCode)) return "Postal code is required.";
        return null;
    }
}
