using System.Globalization;
using System.Text;
using Pineda.Facturacion.Application.Abstractions.Pac;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Application.Contracts.Pac;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public class StampFiscalDocumentService
{
    private static readonly string[] GlobalPeriodicityFieldCodes =
    [
        "INFORMACION_GLOBAL_PERIODICIDAD",
        "GLOBAL_PERIODICIDAD",
        "PERIODICIDAD"
    ];
    private static readonly string[] GlobalMonthsFieldCodes =
    [
        "INFORMACION_GLOBAL_MESES",
        "GLOBAL_MESES",
        "MESES"
    ];
    private static readonly string[] GlobalYearFieldCodes =
    [
        "INFORMACION_GLOBAL_ANIO",
        "INFORMACION_GLOBAL_ANO",
        "GLOBAL_ANIO",
        "GLOBAL_ANO",
        "ANIO",
        "ANO"
    ];

    private readonly IFiscalDocumentRepository _fiscalDocumentRepository;
    private readonly IFiscalStampRepository _fiscalStampRepository;
    private readonly IFiscalStampingGateway _fiscalStampingGateway;
    private readonly IUnitOfWork _unitOfWork;

    public StampFiscalDocumentService(
        IFiscalDocumentRepository fiscalDocumentRepository,
        IFiscalStampRepository fiscalStampRepository,
        IFiscalStampingGateway fiscalStampingGateway,
        IUnitOfWork unitOfWork)
    {
        _fiscalDocumentRepository = fiscalDocumentRepository;
        _fiscalStampRepository = fiscalStampRepository;
        _fiscalStampingGateway = fiscalStampingGateway;
        _unitOfWork = unitOfWork;
    }

    public async Task<StampFiscalDocumentResult> ExecuteAsync(
        StampFiscalDocumentCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.FiscalDocumentId <= 0)
        {
            return ValidationFailure(command.FiscalDocumentId, "Fiscal document id is required.");
        }

        var fiscalDocument = await _fiscalDocumentRepository.GetTrackedByIdAsync(command.FiscalDocumentId, cancellationToken);
        if (fiscalDocument is null)
        {
            return new StampFiscalDocumentResult
            {
                Outcome = StampFiscalDocumentOutcome.NotFound,
                IsSuccess = false,
                FiscalDocumentId = command.FiscalDocumentId,
                ErrorMessage = $"Fiscal document '{command.FiscalDocumentId}' was not found."
            };
        }

        if (fiscalDocument.Status == FiscalDocumentStatus.Stamped)
        {
            return Conflict(fiscalDocument, "Fiscal document is already stamped.");
        }

        if (FiscalOperationRobustnessPolicy.IsStampInProgress(fiscalDocument.Status))
        {
            return Conflict(fiscalDocument, "A stamp request is already in progress for this fiscal document.");
        }

        if (fiscalDocument.Status == FiscalDocumentStatus.StampingRejected && !command.RetryRejected)
        {
            return Conflict(fiscalDocument, "Fiscal document was previously rejected. Set retryRejected to true to retry stamping.");
        }

        if (!FiscalOperationRobustnessPolicy.CanStamp(fiscalDocument.Status))
        {
            return Conflict(fiscalDocument, $"Fiscal document status '{fiscalDocument.Status}' is not eligible for stamping.");
        }

        var existingStamp = await _fiscalStampRepository.GetTrackedByFiscalDocumentIdAsync(command.FiscalDocumentId, cancellationToken);
        if (existingStamp is not null && existingStamp.Status == FiscalStampStatus.Succeeded && !string.IsNullOrWhiteSpace(existingStamp.Uuid))
        {
            return new StampFiscalDocumentResult
            {
                Outcome = StampFiscalDocumentOutcome.Conflict,
                IsSuccess = false,
                FiscalDocumentId = fiscalDocument.Id,
                FiscalDocumentStatus = fiscalDocument.Status,
                FiscalStampId = existingStamp.Id,
                Uuid = existingStamp.Uuid,
                StampedAtUtc = existingStamp.StampedAtUtc,
                ProviderName = existingStamp.ProviderName,
                ProviderTrackingId = existingStamp.ProviderTrackingId,
                ProviderCode = existingStamp.ProviderCode,
                ProviderMessage = existingStamp.ProviderMessage,
                ErrorCode = existingStamp.ErrorCode,
                SupportMessage = FiscalOperationRobustnessPolicy.BuildStampSupportMessage(
                    existingStamp.ProviderCode,
                    existingStamp.ProviderMessage,
                    existingStamp.ErrorCode,
                    existingStamp.ProviderTrackingId),
                RawResponseSummaryJson = existingStamp.RawResponseSummaryJson,
                IsRetryable = false,
                RetryAdvice = FiscalOperationRobustnessPolicy.BuildRetryAdvice(StampFiscalDocumentOutcome.Conflict),
                ErrorMessage = "Fiscal document already has a successful persisted stamp."
            };
        }

        if (!TryBuildStampingRequest(fiscalDocument, out var stampingRequest, out var validationError))
        {
            return ValidationFailure(fiscalDocument.Id, validationError!);
        }

        var requestStartedAtUtc = DateTime.UtcNow;
        fiscalDocument.Status = FiscalDocumentStatus.StampingRequested;
        fiscalDocument.UpdatedAtUtc = requestStartedAtUtc;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        FiscalStampingGatewayResult gatewayResult;

        try
        {
            gatewayResult = await _fiscalStampingGateway.StampAsync(stampingRequest!, cancellationToken);
        }
        catch
        {
            gatewayResult = new FiscalStampingGatewayResult
            {
                Outcome = FiscalStampingGatewayOutcome.Unavailable,
                ProviderName = "FacturaloPlus",
                ProviderOperation = "stamp",
                ErrorMessage = "Provider transport failure.",
                RawResponseSummaryJson = "{\"error\":\"provider_transport_failure\"}"
            };
        }

        var now = DateTime.UtcNow;
        var fiscalStamp = existingStamp ?? new FiscalStamp
        {
            FiscalDocumentId = fiscalDocument.Id,
            CreatedAtUtc = now
        };

        ApplyGatewayResult(fiscalStamp, gatewayResult, now);

        StampFiscalDocumentResult result;
        switch (gatewayResult.Outcome)
        {
            case FiscalStampingGatewayOutcome.Stamped:
                fiscalDocument.Status = FiscalDocumentStatus.Stamped;
                result = Success(fiscalDocument, fiscalStamp);
                break;
            case FiscalStampingGatewayOutcome.Rejected:
                fiscalDocument.Status = FiscalDocumentStatus.StampingRejected;
                result = Failure(StampFiscalDocumentOutcome.ProviderRejected, fiscalDocument, fiscalStamp, gatewayResult.ErrorMessage ?? gatewayResult.ProviderMessage ?? "Provider rejected the stamp request.");
                break;
            case FiscalStampingGatewayOutcome.ValidationFailed:
                fiscalDocument.Status = FiscalDocumentStatus.ReadyForStamping;
                result = Failure(StampFiscalDocumentOutcome.ValidationFailed, fiscalDocument, fiscalStamp, gatewayResult.ErrorMessage ?? "Stamp request validation failed.");
                break;
            default:
                fiscalDocument.Status = FiscalDocumentStatus.ReadyForStamping;
                result = Failure(StampFiscalDocumentOutcome.ProviderUnavailable, fiscalDocument, fiscalStamp, gatewayResult.ErrorMessage ?? "Provider unavailable.");
                break;
        }

        fiscalDocument.UpdatedAtUtc = now;

        if (existingStamp is null)
        {
            await _fiscalStampRepository.AddAsync(fiscalStamp, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        result.FiscalDocumentStatus = fiscalDocument.Status;
        result.FiscalStampId = fiscalStamp.Id;
        result.Uuid = fiscalStamp.Uuid;
        result.StampedAtUtc = fiscalStamp.StampedAtUtc;
        result.ProviderName = fiscalStamp.ProviderName;
        result.ProviderTrackingId = fiscalStamp.ProviderTrackingId;
        result.ProviderCode = fiscalStamp.ProviderCode;
        result.ProviderMessage = fiscalStamp.ProviderMessage;
        result.ErrorCode = fiscalStamp.ErrorCode;
        result.SupportMessage = FiscalOperationRobustnessPolicy.BuildStampSupportMessage(
            fiscalStamp.ProviderCode,
            fiscalStamp.ProviderMessage,
            fiscalStamp.ErrorCode,
            fiscalStamp.ProviderTrackingId);
        result.RawResponseSummaryJson = fiscalStamp.RawResponseSummaryJson;
        result.IsRetryable = FiscalOperationRobustnessPolicy.IsRetryable(result.Outcome);
        result.RetryAdvice = FiscalOperationRobustnessPolicy.BuildRetryAdvice(result.Outcome);
        return result;
    }

    private static void ApplyGatewayResult(FiscalStamp fiscalStamp, FiscalStampingGatewayResult gatewayResult, DateTime now)
    {
        fiscalStamp.ProviderName = gatewayResult.ProviderName;
        fiscalStamp.ProviderOperation = gatewayResult.ProviderOperation;
        fiscalStamp.ProviderRequestHash = gatewayResult.ProviderRequestHash;
        fiscalStamp.ProviderTrackingId = gatewayResult.ProviderTrackingId;
        fiscalStamp.ProviderCode = gatewayResult.ProviderCode;
        fiscalStamp.ProviderMessage = gatewayResult.ProviderMessage;
        fiscalStamp.Uuid = gatewayResult.Uuid;
        fiscalStamp.StampedAtUtc = gatewayResult.StampedAtUtc;
        fiscalStamp.XmlContent = gatewayResult.XmlContent;
        fiscalStamp.XmlHash = gatewayResult.XmlHash;
        fiscalStamp.OriginalString = gatewayResult.OriginalString;
        fiscalStamp.QrCodeTextOrUrl = gatewayResult.QrCodeTextOrUrl;
        fiscalStamp.RawResponseSummaryJson = gatewayResult.RawResponseSummaryJson;
        fiscalStamp.ErrorCode = gatewayResult.ErrorCode;
        fiscalStamp.ErrorMessage = gatewayResult.ErrorMessage;
        fiscalStamp.Status = gatewayResult.Outcome switch
        {
            FiscalStampingGatewayOutcome.Stamped => FiscalStampStatus.Succeeded,
            FiscalStampingGatewayOutcome.Rejected => FiscalStampStatus.Rejected,
            FiscalStampingGatewayOutcome.ValidationFailed => FiscalStampStatus.ValidationFailed,
            _ => FiscalStampStatus.Unavailable
        };
        fiscalStamp.UpdatedAtUtc = now;
    }

    private static bool TryBuildStampingRequest(
        FiscalDocument fiscalDocument,
        out FiscalStampingRequest? request,
        out string? validationError)
    {
        request = null;
        validationError = null;

        if (string.IsNullOrWhiteSpace(fiscalDocument.PacEnvironment))
        {
            validationError = "Fiscal document PAC environment reference is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(fiscalDocument.CertificateReference))
        {
            validationError = "Fiscal document certificate reference is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(fiscalDocument.PrivateKeyReference))
        {
            validationError = "Fiscal document private key reference is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(fiscalDocument.PrivateKeyPasswordReference))
        {
            validationError = "Fiscal document private key password reference is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(fiscalDocument.PaymentMethodSat))
        {
            validationError = "Fiscal document payment method SAT is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(fiscalDocument.PaymentFormSat))
        {
            validationError = "Fiscal document payment form SAT is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(fiscalDocument.CurrencyCode))
        {
            validationError = "Fiscal document currency code is required.";
            return false;
        }

        var currencyCode = FiscalMasterDataNormalization.NormalizeRequiredCode(fiscalDocument.CurrencyCode);
        if (currencyCode != "MXN")
        {
            validationError = $"Current MVP stamping supports MXN only. Fiscal document currency '{currencyCode}' is not supported yet.";
            return false;
        }

        if (!fiscalDocument.Items.Any())
        {
            validationError = "Fiscal document must contain at least one item for stamping.";
            return false;
        }

        var items = new List<FiscalStampingRequestItem>();
        foreach (var item in fiscalDocument.Items.OrderBy(x => x.LineNumber))
        {
            if (string.IsNullOrWhiteSpace(item.InternalCode)
                || string.IsNullOrWhiteSpace(item.Description)
                || string.IsNullOrWhiteSpace(item.SatProductServiceCode)
                || string.IsNullOrWhiteSpace(item.SatUnitCode)
                || string.IsNullOrWhiteSpace(item.TaxObjectCode))
            {
                validationError = $"Fiscal document item line '{item.LineNumber}' is missing required stampable snapshot data.";
                return false;
            }

            items.Add(new FiscalStampingRequestItem
            {
                LineNumber = item.LineNumber,
                InternalCode = item.InternalCode,
                Description = item.Description,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                DiscountAmount = item.DiscountAmount,
                Subtotal = item.Subtotal,
                TaxTotal = item.TaxTotal,
                Total = item.Total,
                SatProductServiceCode = item.SatProductServiceCode,
                SatUnitCode = item.SatUnitCode,
                TaxObjectCode = item.TaxObjectCode,
                VatRate = item.VatRate,
                UnitText = item.UnitText
            });
        }

        FiscalStampingGlobalInformation? globalInformation = null;
        if (RequiresGlobalInformation(fiscalDocument))
        {
            if (!TryBuildGlobalInformation(fiscalDocument.SpecialFieldValues, out globalInformation, out validationError))
            {
                return false;
            }
        }

        request = new FiscalStampingRequest
        {
            FiscalDocumentId = fiscalDocument.Id,
            PacEnvironment = FiscalMasterDataNormalization.NormalizeRequiredCode(fiscalDocument.PacEnvironment),
            CertificateReference = FiscalMasterDataNormalization.NormalizeRequiredText(fiscalDocument.CertificateReference),
            PrivateKeyReference = FiscalMasterDataNormalization.NormalizeRequiredText(fiscalDocument.PrivateKeyReference),
            PrivateKeyPasswordReference = FiscalMasterDataNormalization.NormalizeRequiredText(fiscalDocument.PrivateKeyPasswordReference),
            CfdiVersion = FiscalMasterDataNormalization.NormalizeRequiredText(fiscalDocument.CfdiVersion),
            DocumentType = FiscalMasterDataNormalization.NormalizeRequiredCode(fiscalDocument.DocumentType),
            Series = FiscalMasterDataNormalization.NormalizeOptionalText(fiscalDocument.Series),
            Folio = FiscalMasterDataNormalization.NormalizeOptionalText(fiscalDocument.Folio),
            IssuedAtUtc = fiscalDocument.IssuedAtUtc,
            CurrencyCode = currencyCode,
            ExchangeRate = 1m,
            PaymentMethodSat = FiscalMasterDataNormalization.NormalizeRequiredCode(fiscalDocument.PaymentMethodSat),
            PaymentFormSat = FiscalMasterDataNormalization.NormalizeRequiredCode(fiscalDocument.PaymentFormSat),
            PaymentCondition = FiscalMasterDataNormalization.NormalizeOptionalText(fiscalDocument.PaymentCondition),
            IsCreditSale = fiscalDocument.IsCreditSale,
            CreditDays = fiscalDocument.CreditDays,
            IssuerRfc = FiscalMasterDataNormalization.NormalizeRequiredCode(fiscalDocument.IssuerRfc),
            IssuerLegalName = FiscalMasterDataNormalization.NormalizeRequiredText(fiscalDocument.IssuerLegalName),
            IssuerFiscalRegimeCode = FiscalMasterDataNormalization.NormalizeRequiredCode(fiscalDocument.IssuerFiscalRegimeCode),
            IssuerPostalCode = FiscalMasterDataNormalization.NormalizeRequiredCode(fiscalDocument.IssuerPostalCode),
            ReceiverRfc = FiscalMasterDataNormalization.NormalizeRequiredCode(fiscalDocument.ReceiverRfc),
            ReceiverLegalName = FiscalMasterDataNormalization.NormalizeRequiredText(fiscalDocument.ReceiverLegalName),
            ReceiverFiscalRegimeCode = FiscalMasterDataNormalization.NormalizeRequiredCode(fiscalDocument.ReceiverFiscalRegimeCode),
            ReceiverCfdiUseCode = FiscalMasterDataNormalization.NormalizeRequiredCode(fiscalDocument.ReceiverCfdiUseCode),
            ReceiverPostalCode = FiscalMasterDataNormalization.NormalizeRequiredCode(fiscalDocument.ReceiverPostalCode),
            ReceiverCountryCode = FiscalMasterDataNormalization.NormalizeOptionalText(fiscalDocument.ReceiverCountryCode),
            ReceiverForeignTaxRegistration = FiscalMasterDataNormalization.NormalizeOptionalText(fiscalDocument.ReceiverForeignTaxRegistration),
            Subtotal = fiscalDocument.Subtotal,
            DiscountTotal = fiscalDocument.DiscountTotal,
            TaxTotal = fiscalDocument.TaxTotal,
            Total = fiscalDocument.Total,
            GlobalInformation = globalInformation,
            Items = items
        };

        return true;
    }

    private static bool RequiresGlobalInformation(FiscalDocument fiscalDocument)
    {
        return IsIncomeDocumentType(fiscalDocument.DocumentType)
            && string.Equals(FiscalMasterDataNormalization.NormalizeRfc(fiscalDocument.ReceiverRfc), "XAXX010101000", StringComparison.Ordinal)
            && string.Equals(NormalizePublicGeneralName(fiscalDocument.ReceiverLegalName), "PUBLICO EN GENERAL", StringComparison.Ordinal);
    }

    private static bool TryBuildGlobalInformation(
        IReadOnlyList<FiscalDocumentSpecialFieldValue> specialFieldValues,
        out FiscalStampingGlobalInformation? globalInformation,
        out string? validationError)
    {
        globalInformation = null;
        validationError = null;

        var periodicity = ResolveSpecialFieldValue(specialFieldValues, GlobalPeriodicityFieldCodes);
        var months = ResolveSpecialFieldValue(specialFieldValues, GlobalMonthsFieldCodes);
        var year = ResolveSpecialFieldValue(specialFieldValues, GlobalYearFieldCodes);

        var missingFields = new List<string>();
        if (string.IsNullOrWhiteSpace(periodicity))
        {
            missingFields.Add("Periodicidad");
        }

        if (string.IsNullOrWhiteSpace(months))
        {
            missingFields.Add("Meses");
        }

        if (string.IsNullOrWhiteSpace(year))
        {
            missingFields.Add("Año");
        }

        if (missingFields.Count > 0)
        {
            validationError = $"El CFDI global para PUBLICO EN GENERAL requiere InformacionGlobal. Faltan los campos: {string.Join(", ", missingFields)}.";
            return false;
        }

        globalInformation = new FiscalStampingGlobalInformation
        {
            Periodicity = periodicity!,
            Months = months!,
            Year = year!
        };

        return true;
    }

    private static string? ResolveSpecialFieldValue(
        IReadOnlyList<FiscalDocumentSpecialFieldValue> specialFieldValues,
        IReadOnlyCollection<string> acceptedCodes)
    {
        foreach (var specialFieldValue in specialFieldValues)
        {
            if (!acceptedCodes.Contains(NormalizeSpecialFieldCode(specialFieldValue.FieldCode), StringComparer.Ordinal))
            {
                continue;
            }

            var value = FiscalMasterDataNormalization.NormalizeOptionalText(specialFieldValue.Value);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string NormalizeSpecialFieldCode(string value)
    {
        return RemoveDiacritics(FiscalMasterDataNormalization.NormalizeRequiredText(value)).ToUpperInvariant();
    }

    private static bool IsIncomeDocumentType(string? documentType)
    {
        var normalizedDocumentType = FiscalMasterDataNormalization.NormalizeRequiredCode(documentType ?? string.Empty);
        return string.Equals(normalizedDocumentType, "I", StringComparison.Ordinal)
            || string.Equals(normalizedDocumentType, "INVOICE", StringComparison.Ordinal);
    }

    private static string NormalizePublicGeneralName(string value)
    {
        var trimmed = FiscalMasterDataNormalization.NormalizeRequiredText(value);
        var collapsedWhitespace = string.Join(' ', trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return RemoveDiacritics(collapsedWhitespace).ToUpperInvariant();
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

    private static StampFiscalDocumentResult Success(FiscalDocument fiscalDocument, FiscalStamp fiscalStamp)
    {
        return new StampFiscalDocumentResult
        {
            Outcome = StampFiscalDocumentOutcome.Stamped,
            IsSuccess = true,
            FiscalDocumentId = fiscalDocument.Id,
            FiscalDocumentStatus = FiscalDocumentStatus.Stamped,
            FiscalStampId = fiscalStamp.Id,
            Uuid = fiscalStamp.Uuid,
            StampedAtUtc = fiscalStamp.StampedAtUtc,
            ProviderName = fiscalStamp.ProviderName,
            ProviderTrackingId = fiscalStamp.ProviderTrackingId,
            ProviderCode = fiscalStamp.ProviderCode,
            ProviderMessage = fiscalStamp.ProviderMessage,
            ErrorCode = fiscalStamp.ErrorCode,
            SupportMessage = FiscalOperationRobustnessPolicy.BuildStampSupportMessage(
                fiscalStamp.ProviderCode,
                fiscalStamp.ProviderMessage,
                fiscalStamp.ErrorCode,
                fiscalStamp.ProviderTrackingId),
            RawResponseSummaryJson = fiscalStamp.RawResponseSummaryJson
        };
    }

    private static StampFiscalDocumentResult Failure(
        StampFiscalDocumentOutcome outcome,
        FiscalDocument fiscalDocument,
        FiscalStamp fiscalStamp,
        string errorMessage)
    {
        return new StampFiscalDocumentResult
        {
            Outcome = outcome,
            IsSuccess = false,
            ErrorMessage = errorMessage,
            FiscalDocumentId = fiscalDocument.Id,
            FiscalDocumentStatus = fiscalDocument.Status,
            FiscalStampId = fiscalStamp.Id,
            Uuid = fiscalStamp.Uuid,
            StampedAtUtc = fiscalStamp.StampedAtUtc,
            ProviderName = fiscalStamp.ProviderName,
            ProviderTrackingId = fiscalStamp.ProviderTrackingId,
            ProviderCode = fiscalStamp.ProviderCode,
            ProviderMessage = fiscalStamp.ProviderMessage,
            ErrorCode = fiscalStamp.ErrorCode,
            SupportMessage = FiscalOperationRobustnessPolicy.BuildStampSupportMessage(
                fiscalStamp.ProviderCode,
                fiscalStamp.ProviderMessage,
                fiscalStamp.ErrorCode,
                fiscalStamp.ProviderTrackingId),
            RawResponseSummaryJson = fiscalStamp.RawResponseSummaryJson,
            IsRetryable = FiscalOperationRobustnessPolicy.IsRetryable(outcome),
            RetryAdvice = FiscalOperationRobustnessPolicy.BuildRetryAdvice(outcome)
        };
    }

    private static StampFiscalDocumentResult Conflict(FiscalDocument fiscalDocument, string errorMessage)
    {
        return new StampFiscalDocumentResult
        {
            Outcome = StampFiscalDocumentOutcome.Conflict,
            IsSuccess = false,
            ErrorMessage = errorMessage,
            FiscalDocumentId = fiscalDocument.Id,
            FiscalDocumentStatus = fiscalDocument.Status,
            IsRetryable = false,
            RetryAdvice = FiscalOperationRobustnessPolicy.BuildRetryAdvice(StampFiscalDocumentOutcome.Conflict)
        };
    }

    private static StampFiscalDocumentResult ValidationFailure(long fiscalDocumentId, string errorMessage)
    {
        return new StampFiscalDocumentResult
        {
            Outcome = StampFiscalDocumentOutcome.ValidationFailed,
            IsSuccess = false,
            FiscalDocumentId = fiscalDocumentId,
            ErrorMessage = errorMessage,
            IsRetryable = false,
            RetryAdvice = FiscalOperationRobustnessPolicy.BuildRetryAdvice(StampFiscalDocumentOutcome.ValidationFailed)
        };
    }
}
