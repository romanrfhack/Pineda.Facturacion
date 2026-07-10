using Pineda.Facturacion.Application.Abstractions.FiscalReceivers;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;

namespace Pineda.Facturacion.Application.UseCases.FiscalReceivers;

public class UpdateFiscalReceiverService
{
    private readonly IFiscalReceiverRepository _fiscalReceiverRepository;
    private readonly IFiscalReceiverSatCatalogProvider _fiscalReceiverSatCatalogProvider;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateFiscalReceiverService(
        IFiscalReceiverRepository fiscalReceiverRepository,
        IFiscalReceiverSatCatalogProvider fiscalReceiverSatCatalogProvider,
        IUnitOfWork unitOfWork)
    {
        _fiscalReceiverRepository = fiscalReceiverRepository;
        _fiscalReceiverSatCatalogProvider = fiscalReceiverSatCatalogProvider;
        _unitOfWork = unitOfWork;
    }

    public async Task<UpdateFiscalReceiverResult> ExecuteAsync(UpdateFiscalReceiverCommand command, CancellationToken cancellationToken = default)
    {
        if (command.Id <= 0)
        {
            return ValidationFailure("Fiscal receiver id is required.");
        }

        var validationError = Validate(command, _fiscalReceiverSatCatalogProvider);
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

        var specialFieldConflict = await ApplySpecialFieldsAsync(fiscalReceiver, command.SpecialFields, cancellationToken);
        if (specialFieldConflict is not null)
        {
            return new UpdateFiscalReceiverResult
            {
                Outcome = UpdateFiscalReceiverOutcome.Conflict,
                IsSuccess = false,
                ErrorMessage = specialFieldConflict
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
        fiscalReceiver.Email = CreateFiscalReceiverService.NormalizeEmail(command.Email);
        fiscalReceiver.Phone = FiscalMasterDataNormalization.NormalizeOptionalText(command.Phone);
        fiscalReceiver.SearchAlias = normalizedSearchAlias;
        fiscalReceiver.NormalizedSearchAlias = normalizedSearchAlias is null
            ? null
            : FiscalMasterDataNormalization.NormalizeSearchableText(normalizedSearchAlias);
        fiscalReceiver.IsActive = command.IsActive;
        fiscalReceiver.UpdatedAtUtc = DateTime.UtcNow;
        try
        {
            await _fiscalReceiverRepository.UpdateAsync(fiscalReceiver, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (FiscalReceiverSpecialFieldDefinitionConflictException exception)
        {
            return new UpdateFiscalReceiverResult
            {
                Outcome = UpdateFiscalReceiverOutcome.Conflict,
                IsSuccess = false,
                ErrorMessage = exception.Message
            };
        }

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

    private static string? Validate(UpdateFiscalReceiverCommand command, IFiscalReceiverSatCatalogProvider fiscalReceiverSatCatalogProvider)
    {
        if (string.IsNullOrWhiteSpace(command.Rfc)) return "RFC is required.";
        if (string.IsNullOrWhiteSpace(command.LegalName)) return "Legal name is required.";
        if (string.IsNullOrWhiteSpace(command.FiscalRegimeCode)) return "Fiscal regime code is required.";
        if (string.IsNullOrWhiteSpace(command.CfdiUseCodeDefault)) return "Default CFDI use code is required.";
        if (string.IsNullOrWhiteSpace(command.PostalCode)) return "Postal code is required.";
        var satCatalogValidationError = FiscalReceiverSatCatalogValidation.ValidateCodes(command.FiscalRegimeCode, command.CfdiUseCodeDefault, fiscalReceiverSatCatalogProvider);
        if (satCatalogValidationError is not null) return satCatalogValidationError;
        var emailValidationError = CreateFiscalReceiverService.ValidateEmail(command.Email);
        if (emailValidationError is not null) return emailValidationError;
        var specialFieldValidationError = CreateFiscalReceiverService.ValidateSpecialFields(command.SpecialFields);
        if (specialFieldValidationError is not null) return specialFieldValidationError;
        return null;
    }

    private async Task<string?> ApplySpecialFieldsAsync(
        Domain.Entities.FiscalReceiver fiscalReceiver,
        IReadOnlyList<UpsertFiscalReceiverSpecialFieldDefinitionCommand>? fields,
        CancellationToken cancellationToken)
    {
        if (fields is null || CreateFiscalReceiverService.ContainsOnlyEmptySpecialFieldPlaceholders(fields))
        {
            return null;
        }

        var normalizedFields = CreateFiscalReceiverService.NormalizeSpecialFields(fields);
        var existingById = fiscalReceiver.SpecialFieldDefinitions.ToDictionary(x => x.Id);

        foreach (var field in normalizedFields.Where(x => x.Id is not null))
        {
            if (!existingById.ContainsKey(field.Id!.Value))
            {
                return $"Special field definition '{field.Id}' does not belong to fiscal receiver '{fiscalReceiver.Id}'.";
            }
        }

        var existingCodes = fiscalReceiver.SpecialFieldDefinitions
            .ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);
        foreach (var field in normalizedFields.Where(x => x.Id is null))
        {
            if (existingCodes.ContainsKey(field.Code))
            {
                return $"Special field code '{field.Code}' already exists for this fiscal receiver. Send its id to update it.";
            }
        }

        var incomingIds = normalizedFields.Where(x => x.Id is not null).Select(x => x.Id!.Value).ToHashSet();
        var definitionsToRemove = fiscalReceiver.SpecialFieldDefinitions
            .Where(x => !incomingIds.Contains(x.Id))
            .ToArray();

        foreach (var definition in definitionsToRemove)
        {
            if (await _fiscalReceiverRepository.IsSpecialFieldDefinitionReferencedAsync(definition.Id, cancellationToken))
            {
                return $"No se puede eliminar el campo especial '{definition.Code}' porque ya está referenciado por documentos fiscales.";
            }
        }

        var now = DateTime.UtcNow;
        foreach (var field in normalizedFields)
        {
            if (field.Id is not null)
            {
                var definition = existingById[field.Id.Value];
                definition.Code = field.Code;
                definition.Label = field.Label;
                definition.DataType = field.DataType;
                definition.MaxLength = field.MaxLength;
                definition.HelpText = field.HelpText;
                definition.IsRequired = field.IsRequired;
                definition.IsActive = field.IsActive;
                definition.DisplayOrder = field.DisplayOrder;
                definition.UpdatedAtUtc = now;
                continue;
            }

            fiscalReceiver.SpecialFieldDefinitions.Add(new Domain.Entities.FiscalReceiverSpecialFieldDefinition
            {
                FiscalReceiverId = fiscalReceiver.Id,
                Code = field.Code,
                Label = field.Label,
                DataType = field.DataType,
                MaxLength = field.MaxLength,
                HelpText = field.HelpText,
                IsRequired = field.IsRequired,
                IsActive = field.IsActive,
                DisplayOrder = field.DisplayOrder,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }

        foreach (var definition in definitionsToRemove)
        {
            fiscalReceiver.SpecialFieldDefinitions.Remove(definition);
        }

        return null;
    }
}
