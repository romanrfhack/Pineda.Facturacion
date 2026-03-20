using Pineda.Facturacion.Application.Abstractions.Pac;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Application.Contracts.Pac;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public class StampFiscalDocumentService
{
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

        if (fiscalDocument.Status == FiscalDocumentStatus.StampingRejected && !command.RetryRejected)
        {
            return Conflict(fiscalDocument, "Fiscal document was previously rejected. Set retryRejected to true to retry stamping.");
        }

        if (fiscalDocument.Status is not FiscalDocumentStatus.ReadyForStamping
            && fiscalDocument.Status is not FiscalDocumentStatus.StampingRejected)
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
            Items = items
        };

        return true;
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
            ProviderTrackingId = fiscalStamp.ProviderTrackingId
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
            ProviderTrackingId = fiscalStamp.ProviderTrackingId
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
            FiscalDocumentStatus = fiscalDocument.Status
        };
    }

    private static StampFiscalDocumentResult ValidationFailure(long fiscalDocumentId, string errorMessage)
    {
        return new StampFiscalDocumentResult
        {
            Outcome = StampFiscalDocumentOutcome.ValidationFailed,
            IsSuccess = false,
            FiscalDocumentId = fiscalDocumentId,
            ErrorMessage = errorMessage
        };
    }
}
