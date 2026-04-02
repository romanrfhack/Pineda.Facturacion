using System.Globalization;
using System.Text;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public sealed class SyncFiscalDocumentSpecialFieldsService
{
    private readonly IFiscalDocumentRepository _fiscalDocumentRepository;
    private readonly IFiscalReceiverRepository _fiscalReceiverRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SyncFiscalDocumentSpecialFieldsService(
        IFiscalDocumentRepository fiscalDocumentRepository,
        IFiscalReceiverRepository fiscalReceiverRepository,
        IUnitOfWork unitOfWork)
    {
        _fiscalDocumentRepository = fiscalDocumentRepository;
        _fiscalReceiverRepository = fiscalReceiverRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<SyncFiscalDocumentSpecialFieldsResult> ExecuteAsync(
        SyncFiscalDocumentSpecialFieldsCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.FiscalDocumentId <= 0)
        {
            return ValidationFailure(command.FiscalDocumentId, "Fiscal document id is required.");
        }

        var fiscalDocument = await _fiscalDocumentRepository.GetTrackedByIdAsync(command.FiscalDocumentId, cancellationToken);
        if (fiscalDocument is null)
        {
            return new SyncFiscalDocumentSpecialFieldsResult
            {
                Outcome = SyncFiscalDocumentSpecialFieldsOutcome.NotFound,
                IsSuccess = false,
                FiscalDocumentId = command.FiscalDocumentId,
                ErrorMessage = $"Fiscal document '{command.FiscalDocumentId}' was not found."
            };
        }

        if (!CanSyncSpecialFields(fiscalDocument.Status))
        {
            return new SyncFiscalDocumentSpecialFieldsResult
            {
                Outcome = SyncFiscalDocumentSpecialFieldsOutcome.Conflict,
                IsSuccess = false,
                FiscalDocumentId = fiscalDocument.Id,
                FiscalDocumentStatus = fiscalDocument.Status,
                ErrorMessage = "Fiscal document special fields can only be synchronized while the document is still editable before a successful stamp."
            };
        }

        var fiscalReceiver = await _fiscalReceiverRepository.GetByIdAsync(fiscalDocument.FiscalReceiverId, cancellationToken);
        if (fiscalReceiver is null || !fiscalReceiver.IsActive)
        {
            return ValidationFailure(fiscalDocument.Id, "Fiscal receiver is missing or inactive for this fiscal document.");
        }

        var validationError = ValidateSpecialFields(fiscalReceiver, command.SpecialFields, fiscalDocument.SpecialFieldValues);
        if (validationError is not null)
        {
            return ValidationFailure(fiscalDocument.Id, validationError, fiscalDocument.Status);
        }

        var now = DateTime.UtcNow;
        fiscalDocument.SpecialFieldValues = BuildSpecialFieldSnapshots(fiscalDocument.Id, fiscalReceiver, command.SpecialFields, fiscalDocument.SpecialFieldValues, now);
        fiscalDocument.UpdatedAtUtc = now;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new SyncFiscalDocumentSpecialFieldsResult
        {
            Outcome = SyncFiscalDocumentSpecialFieldsOutcome.Updated,
            IsSuccess = true,
            FiscalDocumentId = fiscalDocument.Id,
            FiscalDocumentStatus = fiscalDocument.Status,
            SpecialFieldCount = fiscalDocument.SpecialFieldValues.Count
        };
    }

    private static bool CanSyncSpecialFields(FiscalDocumentStatus status)
    {
        return status == FiscalDocumentStatus.ReadyForStamping
            || status == FiscalDocumentStatus.StampingRejected;
    }

    private static string? ValidateSpecialFields(
        FiscalReceiver fiscalReceiver,
        IReadOnlyList<SyncFiscalDocumentSpecialFieldValueCommand>? requestedFields,
        IReadOnlyList<FiscalDocumentSpecialFieldValue> currentValues)
    {
        var activeDefinitions = fiscalReceiver.SpecialFieldDefinitions
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .ToArray();

        if (activeDefinitions.Length == 0)
        {
            return null;
        }

        var requestValuesByCode = BuildValuesByCode(requestedFields);
        var currentValuesByCode = currentValues
            .Where(x => !string.IsNullOrWhiteSpace(x.FieldCode))
            .ToDictionary(
                x => NormalizeSpecialFieldCode(x.FieldCode),
                x => x.Value?.Trim() ?? string.Empty,
                StringComparer.Ordinal);

        foreach (var definition in activeDefinitions)
        {
            var normalizedDefinitionCode = NormalizeSpecialFieldCode(definition.Code);
            var effectiveValue = requestValuesByCode.TryGetValue(normalizedDefinitionCode, out var requestedValue)
                ? requestedValue
                : currentValuesByCode.TryGetValue(normalizedDefinitionCode, out var currentValue)
                    ? currentValue
                    : string.Empty;

            if (definition.IsRequired && string.IsNullOrWhiteSpace(effectiveValue))
            {
                return $"El campo especial '{definition.Label}' es requerido para este documento fiscal.";
            }

            if (definition.MaxLength.HasValue
                && !string.IsNullOrWhiteSpace(effectiveValue)
                && effectiveValue.Length > definition.MaxLength.Value)
            {
                return $"El campo especial '{definition.Label}' excede la longitud máxima permitida de {definition.MaxLength.Value} caracteres.";
            }

            if (!string.IsNullOrWhiteSpace(effectiveValue))
            {
                switch (definition.DataType)
                {
                    case "number" when !decimal.TryParse(effectiveValue, NumberStyles.Any, CultureInfo.InvariantCulture, out _):
                        return $"El campo especial '{definition.Label}' debe ser numérico.";
                    case "date" when !DateOnly.TryParse(effectiveValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out _):
                        return $"El campo especial '{definition.Label}' debe ser una fecha válida.";
                }
            }
        }

        return null;
    }

    private static List<FiscalDocumentSpecialFieldValue> BuildSpecialFieldSnapshots(
        long fiscalDocumentId,
        FiscalReceiver fiscalReceiver,
        IReadOnlyList<SyncFiscalDocumentSpecialFieldValueCommand>? requestedFields,
        IReadOnlyList<FiscalDocumentSpecialFieldValue> currentValues,
        DateTime now)
    {
        var requestValuesByCode = BuildValuesByCode(requestedFields);
        var currentValuesByCode = currentValues
            .Where(x => !string.IsNullOrWhiteSpace(x.FieldCode))
            .ToDictionary(
                x => NormalizeSpecialFieldCode(x.FieldCode),
                x => x,
                StringComparer.Ordinal);

        return fiscalReceiver.SpecialFieldDefinitions
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .Select(definition =>
            {
                var normalizedDefinitionCode = NormalizeSpecialFieldCode(definition.Code);
                var value = requestValuesByCode.TryGetValue(normalizedDefinitionCode, out var requestedValue)
                    ? requestedValue
                    : currentValuesByCode.TryGetValue(normalizedDefinitionCode, out var currentValue)
                        ? currentValue.Value?.Trim() ?? string.Empty
                        : string.Empty;

                return new FiscalDocumentSpecialFieldValue
                {
                    FiscalDocumentId = currentValuesByCode.TryGetValue(normalizedDefinitionCode, out var existingValue)
                        ? existingValue.FiscalDocumentId
                        : fiscalDocumentId,
                    FiscalReceiverSpecialFieldDefinitionId = definition.Id,
                    FieldCode = definition.Code,
                    FieldLabelSnapshot = definition.Label,
                    DataType = definition.DataType,
                    Value = value,
                    DisplayOrder = definition.DisplayOrder,
                    CreatedAtUtc = existingValue?.CreatedAtUtc ?? now
                };
            })
            .ToList();
    }

    private static Dictionary<string, string> BuildValuesByCode(IReadOnlyList<SyncFiscalDocumentSpecialFieldValueCommand>? requestedFields)
    {
        return (requestedFields ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x.FieldCode))
            .ToDictionary(
                x => NormalizeSpecialFieldCode(x.FieldCode),
                x => x.Value?.Trim() ?? string.Empty,
                StringComparer.Ordinal);
    }

    private static string NormalizeSpecialFieldCode(string value)
    {
        return RemoveDiacritics(FiscalMasterDataNormalization.NormalizeRequiredText(value)).ToUpperInvariant();
    }

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder
            .ToString()
            .Normalize(NormalizationForm.FormC);
    }

    private static SyncFiscalDocumentSpecialFieldsResult ValidationFailure(
        long fiscalDocumentId,
        string errorMessage,
        FiscalDocumentStatus? fiscalDocumentStatus = null)
    {
        return new SyncFiscalDocumentSpecialFieldsResult
        {
            Outcome = SyncFiscalDocumentSpecialFieldsOutcome.ValidationFailed,
            IsSuccess = false,
            FiscalDocumentId = fiscalDocumentId,
            FiscalDocumentStatus = fiscalDocumentStatus,
            ErrorMessage = errorMessage
        };
    }
}
