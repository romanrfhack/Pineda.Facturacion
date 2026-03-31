using Pineda.Facturacion.Application.Abstractions.Pac;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Application.Contracts.Pac;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public class StampPaymentComplementService
{
    private readonly IPaymentComplementDocumentRepository _paymentComplementDocumentRepository;
    private readonly IPaymentComplementStampRepository _paymentComplementStampRepository;
    private readonly IPaymentComplementStampingGateway _paymentComplementStampingGateway;
    private readonly IUnitOfWork _unitOfWork;

    public StampPaymentComplementService(
        IPaymentComplementDocumentRepository paymentComplementDocumentRepository,
        IPaymentComplementStampRepository paymentComplementStampRepository,
        IPaymentComplementStampingGateway paymentComplementStampingGateway,
        IUnitOfWork unitOfWork)
    {
        _paymentComplementDocumentRepository = paymentComplementDocumentRepository;
        _paymentComplementStampRepository = paymentComplementStampRepository;
        _paymentComplementStampingGateway = paymentComplementStampingGateway;
        _unitOfWork = unitOfWork;
    }

    public async Task<StampPaymentComplementResult> ExecuteAsync(StampPaymentComplementCommand command, CancellationToken cancellationToken = default)
    {
        if (command.PaymentComplementId <= 0)
        {
            return ValidationFailure(command.PaymentComplementId, "Payment complement id is required.");
        }

        var document = await _paymentComplementDocumentRepository.GetTrackedByIdAsync(command.PaymentComplementId, cancellationToken);
        if (document is null)
        {
            return new StampPaymentComplementResult
            {
                Outcome = StampPaymentComplementOutcome.NotFound,
                IsSuccess = false,
                PaymentComplementId = command.PaymentComplementId,
                ErrorMessage = $"Payment complement '{command.PaymentComplementId}' was not found."
            };
        }

        if (document.Status == PaymentComplementDocumentStatus.Stamped)
        {
            return Conflict(document, "Payment complement is already stamped.");
        }

        if (document.Status == PaymentComplementDocumentStatus.StampingRejected && !command.RetryRejected)
        {
            return Conflict(document, "Payment complement was previously rejected. Set retryRejected to true to retry stamping.");
        }

        if (document.Status is not PaymentComplementDocumentStatus.ReadyForStamping
            && document.Status is not PaymentComplementDocumentStatus.StampingRejected)
        {
            return Conflict(document, $"Payment complement status '{document.Status}' is not eligible for stamping.");
        }

        var existingStamp = await _paymentComplementStampRepository.GetTrackedByPaymentComplementDocumentIdAsync(document.Id, cancellationToken);
        if (existingStamp is not null && existingStamp.Status == FiscalStampStatus.Succeeded && !string.IsNullOrWhiteSpace(existingStamp.Uuid))
        {
            return new StampPaymentComplementResult
            {
                Outcome = StampPaymentComplementOutcome.Conflict,
                IsSuccess = false,
                PaymentComplementId = document.Id,
                Status = document.Status,
                PaymentComplementStampId = existingStamp.Id,
                Uuid = existingStamp.Uuid,
                StampedAtUtc = existingStamp.StampedAtUtc,
                ProviderName = existingStamp.ProviderName,
                ProviderTrackingId = existingStamp.ProviderTrackingId,
                ErrorMessage = "Payment complement already has a successful persisted stamp."
            };
        }

        if (!TryBuildRequest(document, out var request, out var validationError))
        {
            return ValidationFailure(document.Id, validationError!);
        }

        var requestStartedAtUtc = DateTime.UtcNow;
        document.Status = PaymentComplementDocumentStatus.StampingRequested;
        document.UpdatedAtUtc = requestStartedAtUtc;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        PaymentComplementStampingGatewayResult gatewayResult;
        try
        {
            gatewayResult = await _paymentComplementStampingGateway.StampAsync(request!, cancellationToken);
        }
        catch
        {
            gatewayResult = new PaymentComplementStampingGatewayResult
            {
                Outcome = PaymentComplementStampingGatewayOutcome.Unavailable,
                ProviderName = "FacturaloPlus",
                ProviderOperation = "payment-complement-stamp",
                ErrorMessage = "Provider transport failure.",
                RawResponseSummaryJson = "{\"error\":\"provider_transport_failure\"}"
            };
        }

        var now = DateTime.UtcNow;
        var stamp = existingStamp ?? new PaymentComplementStamp
        {
            PaymentComplementDocumentId = document.Id,
            CreatedAtUtc = now
        };

        ApplyGatewayResult(stamp, gatewayResult, now);

        StampPaymentComplementResult result;
        switch (gatewayResult.Outcome)
        {
            case PaymentComplementStampingGatewayOutcome.Stamped:
                document.Status = PaymentComplementDocumentStatus.Stamped;
                document.ProviderName = stamp.ProviderName;
                result = Success(document, stamp);
                break;
            case PaymentComplementStampingGatewayOutcome.Rejected:
                document.Status = PaymentComplementDocumentStatus.StampingRejected;
                document.ProviderName = stamp.ProviderName;
                result = Failure(StampPaymentComplementOutcome.ProviderRejected, document, stamp, gatewayResult.ErrorMessage ?? gatewayResult.ProviderMessage ?? "Provider rejected the payment complement stamp request.");
                break;
            case PaymentComplementStampingGatewayOutcome.ValidationFailed:
                document.Status = PaymentComplementDocumentStatus.ReadyForStamping;
                result = Failure(StampPaymentComplementOutcome.ValidationFailed, document, stamp, gatewayResult.ErrorMessage ?? "Payment complement stamp request validation failed.");
                break;
            default:
                document.Status = PaymentComplementDocumentStatus.ReadyForStamping;
                result = Failure(StampPaymentComplementOutcome.ProviderUnavailable, document, stamp, gatewayResult.ErrorMessage ?? "Provider unavailable.");
                break;
        }

        document.UpdatedAtUtc = now;
        if (existingStamp is null)
        {
            await _paymentComplementStampRepository.AddAsync(stamp, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        result.Status = document.Status;
        result.PaymentComplementStampId = stamp.Id;
        result.Uuid = stamp.Uuid;
        result.StampedAtUtc = stamp.StampedAtUtc;
        result.ProviderName = stamp.ProviderName;
        result.ProviderTrackingId = stamp.ProviderTrackingId;
        result.ProviderCode = stamp.ProviderCode;
        result.ProviderMessage = stamp.ProviderMessage;
        result.ErrorCode = stamp.ErrorCode;
        result.RawResponseSummaryJson = stamp.RawResponseSummaryJson;
        result.SupportMessage = BuildStampSupportMessage(stamp);
        return result;
    }

    private static bool TryBuildRequest(PaymentComplementDocument document, out PaymentComplementStampingRequest? request, out string? validationError)
    {
        request = null;
        validationError = null;

        if (string.IsNullOrWhiteSpace(document.PacEnvironment))
        {
            validationError = "Payment complement PAC environment reference is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(document.CertificateReference))
        {
            validationError = "Payment complement certificate reference is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(document.PrivateKeyReference))
        {
            validationError = "Payment complement private key reference is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(document.PrivateKeyPasswordReference))
        {
            validationError = "Payment complement private key password reference is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(document.CurrencyCode) || !string.Equals(FiscalMasterDataNormalization.NormalizeRequiredCode(document.CurrencyCode), "MXN", StringComparison.Ordinal))
        {
            validationError = "Current MVP payment complement stamping supports MXN only.";
            return false;
        }

        if (!document.RelatedDocuments.Any())
        {
            validationError = "Payment complement must contain at least one related document.";
            return false;
        }

        if (document.RelatedDocuments.Any(x => string.IsNullOrWhiteSpace(x.RelatedDocumentUuid)))
        {
            validationError = "Payment complement related documents must contain persisted original invoice UUIDs.";
            return false;
        }

        request = new PaymentComplementStampingRequest
        {
            PaymentComplementDocumentId = document.Id,
            PacEnvironment = document.PacEnvironment,
            CertificateReference = document.CertificateReference,
            PrivateKeyReference = document.PrivateKeyReference,
            PrivateKeyPasswordReference = document.PrivateKeyPasswordReference,
            CfdiVersion = document.CfdiVersion,
            DocumentType = document.DocumentType,
            IssuedAtUtc = document.IssuedAtUtc,
            PaymentDateUtc = document.PaymentDateUtc,
            CurrencyCode = "MXN",
            TotalPaymentsAmount = document.TotalPaymentsAmount,
            IssuerRfc = document.IssuerRfc,
            IssuerLegalName = document.IssuerLegalName,
            IssuerFiscalRegimeCode = document.IssuerFiscalRegimeCode,
            IssuerPostalCode = document.IssuerPostalCode,
            ReceiverRfc = document.ReceiverRfc,
            ReceiverLegalName = document.ReceiverLegalName,
            ReceiverFiscalRegimeCode = document.ReceiverFiscalRegimeCode,
            ReceiverPostalCode = document.ReceiverPostalCode,
            ReceiverCountryCode = document.ReceiverCountryCode,
            ReceiverForeignTaxRegistration = document.ReceiverForeignTaxRegistration,
            RelatedDocuments = document.RelatedDocuments
                .OrderBy(x => x.Id)
                .Select(x => new PaymentComplementStampingRequestRelatedDocument
                {
                    AccountsReceivableInvoiceId = x.AccountsReceivableInvoiceId,
                    FiscalDocumentId = x.FiscalDocumentId,
                    RelatedDocumentUuid = x.RelatedDocumentUuid,
                    InstallmentNumber = x.InstallmentNumber,
                    PreviousBalance = x.PreviousBalance,
                    PaidAmount = x.PaidAmount,
                    RemainingBalance = x.RemainingBalance,
                    CurrencyCode = x.CurrencyCode
                })
                .ToList()
        };

        return true;
    }

    private static void ApplyGatewayResult(PaymentComplementStamp stamp, PaymentComplementStampingGatewayResult gatewayResult, DateTime now)
    {
        stamp.ProviderName = gatewayResult.ProviderName;
        stamp.ProviderOperation = gatewayResult.ProviderOperation;
        stamp.ProviderRequestHash = gatewayResult.ProviderRequestHash;
        stamp.ProviderTrackingId = gatewayResult.ProviderTrackingId;
        stamp.ProviderCode = gatewayResult.ProviderCode;
        stamp.ProviderMessage = gatewayResult.ProviderMessage;
        stamp.Uuid = gatewayResult.Uuid;
        stamp.StampedAtUtc = gatewayResult.StampedAtUtc;
        stamp.XmlContent = gatewayResult.XmlContent;
        stamp.XmlHash = gatewayResult.XmlHash;
        stamp.OriginalString = gatewayResult.OriginalString;
        stamp.QrCodeTextOrUrl = gatewayResult.QrCodeTextOrUrl;
        stamp.RawResponseSummaryJson = gatewayResult.RawResponseSummaryJson;
        stamp.ErrorCode = gatewayResult.ErrorCode;
        stamp.ErrorMessage = gatewayResult.ErrorMessage;
        stamp.Status = gatewayResult.Outcome switch
        {
            PaymentComplementStampingGatewayOutcome.Stamped => FiscalStampStatus.Succeeded,
            PaymentComplementStampingGatewayOutcome.Rejected => FiscalStampStatus.Rejected,
            PaymentComplementStampingGatewayOutcome.ValidationFailed => FiscalStampStatus.ValidationFailed,
            _ => FiscalStampStatus.Unavailable
        };
        stamp.UpdatedAtUtc = now;
    }

    private static StampPaymentComplementResult Success(PaymentComplementDocument document, PaymentComplementStamp stamp)
    {
        return new StampPaymentComplementResult
        {
            Outcome = StampPaymentComplementOutcome.Stamped,
            IsSuccess = true,
            PaymentComplementId = document.Id,
            Status = document.Status,
            PaymentComplementStampId = stamp.Id,
            Uuid = stamp.Uuid,
            StampedAtUtc = stamp.StampedAtUtc,
            ProviderName = stamp.ProviderName,
            ProviderTrackingId = stamp.ProviderTrackingId,
            ProviderCode = stamp.ProviderCode,
            ProviderMessage = stamp.ProviderMessage,
            ErrorCode = stamp.ErrorCode,
            RawResponseSummaryJson = stamp.RawResponseSummaryJson,
            SupportMessage = BuildStampSupportMessage(stamp)
        };
    }

    private static StampPaymentComplementResult Failure(StampPaymentComplementOutcome outcome, PaymentComplementDocument document, PaymentComplementStamp stamp, string errorMessage)
    {
        return new StampPaymentComplementResult
        {
            Outcome = outcome,
            IsSuccess = false,
            PaymentComplementId = document.Id,
            Status = document.Status,
            PaymentComplementStampId = stamp.Id,
            Uuid = stamp.Uuid,
            StampedAtUtc = stamp.StampedAtUtc,
            ProviderName = stamp.ProviderName,
            ProviderTrackingId = stamp.ProviderTrackingId,
            ProviderCode = stamp.ProviderCode,
            ProviderMessage = stamp.ProviderMessage,
            ErrorCode = stamp.ErrorCode,
            RawResponseSummaryJson = stamp.RawResponseSummaryJson,
            SupportMessage = BuildStampSupportMessage(stamp),
            ErrorMessage = errorMessage
        };
    }

    private static StampPaymentComplementResult Conflict(PaymentComplementDocument document, string errorMessage)
    {
        return new StampPaymentComplementResult
        {
            Outcome = StampPaymentComplementOutcome.Conflict,
            IsSuccess = false,
            PaymentComplementId = document.Id,
            Status = document.Status,
            SupportMessage = "El complemento de pago ya no está en un estado elegible para timbrado manual.",
            ErrorMessage = errorMessage
        };
    }

    private static StampPaymentComplementResult ValidationFailure(long paymentComplementId, string errorMessage)
    {
        return new StampPaymentComplementResult
        {
            Outcome = StampPaymentComplementOutcome.ValidationFailed,
            IsSuccess = false,
            PaymentComplementId = paymentComplementId,
            SupportMessage = "La preparación del complemento no es elegible todavía. Revisa que el pago esté aplicado contra CFDI de ingreso PPD/99 ya timbrados.",
            ErrorMessage = errorMessage
        };
    }

    private static string BuildStampSupportMessage(PaymentComplementStamp stamp)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(stamp.ProviderCode))
        {
            parts.Add($"Código proveedor: {stamp.ProviderCode}");
        }

        if (!string.IsNullOrWhiteSpace(stamp.ProviderMessage))
        {
            parts.Add($"Mensaje proveedor: {stamp.ProviderMessage}");
        }

        if (!string.IsNullOrWhiteSpace(stamp.ErrorCode))
        {
            parts.Add($"Error: {stamp.ErrorCode}");
        }

        if (!string.IsNullOrWhiteSpace(stamp.ProviderTrackingId))
        {
            parts.Add($"Tracking: {stamp.ProviderTrackingId}");
        }

        if (!string.IsNullOrWhiteSpace(stamp.Uuid))
        {
            parts.Add($"UUID: {stamp.Uuid}");
        }

        return parts.Count == 0
            ? "No hay metadatos adicionales de timbrado del complemento."
            : string.Join(" | ", parts);
    }
}
