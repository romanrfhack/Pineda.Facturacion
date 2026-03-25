using Pineda.Facturacion.Application.Abstractions.FiscalReceivers;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.FiscalReceivers;

public class CreateFiscalReceiverService
{
    private readonly IFiscalReceiverRepository _fiscalReceiverRepository;
    private readonly IFiscalReceiverSatCatalogProvider _fiscalReceiverSatCatalogProvider;
    private readonly IUnitOfWork _unitOfWork;

    public CreateFiscalReceiverService(
        IFiscalReceiverRepository fiscalReceiverRepository,
        IFiscalReceiverSatCatalogProvider fiscalReceiverSatCatalogProvider,
        IUnitOfWork unitOfWork)
    {
        _fiscalReceiverRepository = fiscalReceiverRepository;
        _fiscalReceiverSatCatalogProvider = fiscalReceiverSatCatalogProvider;
        _unitOfWork = unitOfWork;
    }

    public async Task<CreateFiscalReceiverResult> ExecuteAsync(CreateFiscalReceiverCommand command, CancellationToken cancellationToken = default)
    {
        var validationError = Validate(command, _fiscalReceiverSatCatalogProvider);
        if (validationError is not null)
        {
            return new CreateFiscalReceiverResult
            {
                Outcome = CreateFiscalReceiverOutcome.ValidationFailed,
                IsSuccess = false,
                ErrorMessage = validationError
            };
        }

        var normalizedRfc = FiscalMasterDataNormalization.NormalizeRfc(command.Rfc);
        var existing = await _fiscalReceiverRepository.GetByRfcAsync(normalizedRfc, cancellationToken);
        if (existing is not null)
        {
            return new CreateFiscalReceiverResult
            {
                Outcome = CreateFiscalReceiverOutcome.Conflict,
                IsSuccess = false,
                ErrorMessage = $"A fiscal receiver with RFC '{normalizedRfc}' already exists."
            };
        }

        var now = DateTime.UtcNow;
        var normalizedLegalName = FiscalMasterDataNormalization.NormalizeRequiredText(command.LegalName);
        var normalizedSearchAlias = FiscalMasterDataNormalization.NormalizeOptionalText(command.SearchAlias);
        var fiscalReceiver = new FiscalReceiver
        {
            Rfc = normalizedRfc,
            LegalName = normalizedLegalName,
            NormalizedLegalName = FiscalMasterDataNormalization.NormalizeSearchableText(normalizedLegalName),
            FiscalRegimeCode = FiscalMasterDataNormalization.NormalizeRequiredCode(command.FiscalRegimeCode),
            CfdiUseCodeDefault = FiscalMasterDataNormalization.NormalizeRequiredCode(command.CfdiUseCodeDefault),
            PostalCode = FiscalMasterDataNormalization.NormalizeRequiredCode(command.PostalCode),
            CountryCode = FiscalMasterDataNormalization.NormalizeOptionalText(command.CountryCode)?.ToUpperInvariant(),
            ForeignTaxRegistration = FiscalMasterDataNormalization.NormalizeOptionalText(command.ForeignTaxRegistration),
            Email = FiscalMasterDataNormalization.NormalizeOptionalText(command.Email),
            Phone = FiscalMasterDataNormalization.NormalizeOptionalText(command.Phone),
            SearchAlias = normalizedSearchAlias,
            NormalizedSearchAlias = normalizedSearchAlias is null
                ? null
                : FiscalMasterDataNormalization.NormalizeSearchableText(normalizedSearchAlias),
            IsActive = command.IsActive,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            SpecialFieldDefinitions = BuildSpecialFieldDefinitions(command.SpecialFields, now)
        };

        await _fiscalReceiverRepository.AddAsync(fiscalReceiver, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateFiscalReceiverResult
        {
            Outcome = CreateFiscalReceiverOutcome.Created,
            IsSuccess = true,
            FiscalReceiverId = fiscalReceiver.Id
        };
    }

    private static string? Validate(CreateFiscalReceiverCommand command, IFiscalReceiverSatCatalogProvider fiscalReceiverSatCatalogProvider)
    {
        if (string.IsNullOrWhiteSpace(command.Rfc)) return "RFC is required.";
        if (string.IsNullOrWhiteSpace(command.LegalName)) return "Legal name is required.";
        if (string.IsNullOrWhiteSpace(command.FiscalRegimeCode)) return "Fiscal regime code is required.";
        if (string.IsNullOrWhiteSpace(command.CfdiUseCodeDefault)) return "Default CFDI use code is required.";
        if (string.IsNullOrWhiteSpace(command.PostalCode)) return "Postal code is required.";
        var satCatalogValidationError = FiscalReceiverSatCatalogValidation.ValidateCodes(command.FiscalRegimeCode, command.CfdiUseCodeDefault, fiscalReceiverSatCatalogProvider);
        if (satCatalogValidationError is not null) return satCatalogValidationError;
        var specialFieldValidationError = ValidateSpecialFields(command.SpecialFields);
        if (specialFieldValidationError is not null) return specialFieldValidationError;
        return null;
    }

    internal static List<FiscalReceiverSpecialFieldDefinition> BuildSpecialFieldDefinitions(
        IReadOnlyList<UpsertFiscalReceiverSpecialFieldDefinitionCommand>? fields,
        DateTime now)
    {
        return NormalizeSpecialFields(fields)
            .Select(field => new FiscalReceiverSpecialFieldDefinition
            {
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
            })
            .ToList();
    }

    internal static string? ValidateSpecialFields(IReadOnlyList<UpsertFiscalReceiverSpecialFieldDefinitionCommand>? fields)
    {
        try
        {
            var normalized = NormalizeSpecialFields(fields);
            var duplicateCode = normalized
                .GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(x => x.Count() > 1);

            return duplicateCode is null
                ? null
                : $"Special field code '{duplicateCode.Key}' is duplicated for the same receiver.";
        }
        catch (ArgumentException exception)
        {
            return exception.Message;
        }
    }

    internal static IReadOnlyList<NormalizedSpecialFieldDefinition> NormalizeSpecialFields(IReadOnlyList<UpsertFiscalReceiverSpecialFieldDefinitionCommand>? fields)
    {
        if (fields is null || fields.Count == 0)
        {
            return [];
        }

        return fields
            .Select((field, index) => NormalizeSpecialField(field, index))
            .ToArray();
    }

    private static NormalizedSpecialFieldDefinition NormalizeSpecialField(UpsertFiscalReceiverSpecialFieldDefinitionCommand field, int index)
    {
        var code = FiscalMasterDataNormalization.NormalizeRequiredText(field.Code).ToUpperInvariant();
        var label = FiscalMasterDataNormalization.NormalizeRequiredText(field.Label);

        return new NormalizedSpecialFieldDefinition(
            code,
            label,
            NormalizeDataType(field.DataType),
            field.MaxLength is > 0 ? field.MaxLength : null,
            FiscalMasterDataNormalization.NormalizeOptionalText(field.HelpText),
            field.IsRequired,
            field.IsActive,
            field.DisplayOrder > 0 ? field.DisplayOrder : index + 1);
    }

    private static string NormalizeDataType(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "number" => "number",
            "date" => "date",
            _ => "text"
        };
    }

    internal sealed record NormalizedSpecialFieldDefinition(
        string Code,
        string Label,
        string DataType,
        int? MaxLength,
        string? HelpText,
        bool IsRequired,
        bool IsActive,
        int DisplayOrder);
}
