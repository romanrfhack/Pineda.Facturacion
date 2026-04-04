using System.Globalization;
using System.Xml.Linq;
using Pineda.Facturacion.Application.Abstractions.Pac;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Application.Contracts.Pac;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public class StampPaymentComplementService
{
    private const string DefaultTaxCode = "002";
    private readonly IPaymentComplementDocumentRepository _paymentComplementDocumentRepository;
    private readonly IPaymentComplementStampRepository _paymentComplementStampRepository;
    private readonly IAccountsReceivablePaymentRepository _accountsReceivablePaymentRepository;
    private readonly IFiscalDocumentRepository _fiscalDocumentRepository;
    private readonly IExternalRepBaseDocumentRepository _externalRepBaseDocumentRepository;
    private readonly IPaymentComplementStampingGateway _paymentComplementStampingGateway;
    private readonly IUnitOfWork _unitOfWork;

    public StampPaymentComplementService(
        IPaymentComplementDocumentRepository paymentComplementDocumentRepository,
        IPaymentComplementStampRepository paymentComplementStampRepository,
        IAccountsReceivablePaymentRepository accountsReceivablePaymentRepository,
        IFiscalDocumentRepository fiscalDocumentRepository,
        IExternalRepBaseDocumentRepository externalRepBaseDocumentRepository,
        IPaymentComplementStampingGateway paymentComplementStampingGateway,
        IUnitOfWork unitOfWork)
    {
        _paymentComplementDocumentRepository = paymentComplementDocumentRepository;
        _paymentComplementStampRepository = paymentComplementStampRepository;
        _accountsReceivablePaymentRepository = accountsReceivablePaymentRepository;
        _fiscalDocumentRepository = fiscalDocumentRepository;
        _externalRepBaseDocumentRepository = externalRepBaseDocumentRepository;
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

        var requestBuildResult = await TryBuildRequestAsync(document, cancellationToken);
        if (!requestBuildResult.IsSuccess)
        {
            return ValidationFailure(document.Id, requestBuildResult.ErrorMessage!);
        }
        var request = requestBuildResult.Request;

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

    private async Task<RequestBuildResult> TryBuildRequestAsync(
        PaymentComplementDocument document,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(document.PacEnvironment))
        {
            return RequestBuildResult.Fail("Payment complement PAC environment reference is required.");
        }

        if (string.IsNullOrWhiteSpace(document.CertificateReference))
        {
            return RequestBuildResult.Fail("Payment complement certificate reference is required.");
        }

        if (string.IsNullOrWhiteSpace(document.PrivateKeyReference))
        {
            return RequestBuildResult.Fail("Payment complement private key reference is required.");
        }

        if (string.IsNullOrWhiteSpace(document.PrivateKeyPasswordReference))
        {
            return RequestBuildResult.Fail("Payment complement private key password reference is required.");
        }

        if (string.IsNullOrWhiteSpace(document.CurrencyCode) || !string.Equals(FiscalMasterDataNormalization.NormalizeRequiredCode(document.CurrencyCode), "MXN", StringComparison.Ordinal))
        {
            return RequestBuildResult.Fail("Current MVP payment complement stamping supports MXN only.");
        }

        if (!document.RelatedDocuments.Any())
        {
            return RequestBuildResult.Fail("Payment complement must contain at least one related document.");
        }

        if (document.RelatedDocuments.Any(x => string.IsNullOrWhiteSpace(x.RelatedDocumentUuid)))
        {
            return RequestBuildResult.Fail("Payment complement related documents must contain persisted original invoice UUIDs.");
        }

        var payment = await _accountsReceivablePaymentRepository.GetByIdAsync(document.AccountsReceivablePaymentId, cancellationToken);
        if (payment is null)
        {
            return RequestBuildResult.Fail($"Accounts receivable payment '{document.AccountsReceivablePaymentId}' was not found.");
        }

        if (string.IsNullOrWhiteSpace(payment.PaymentFormSat))
        {
            return RequestBuildResult.Fail("Payment complement payment form SAT is required.");
        }

        var relatedDocuments = new List<PaymentComplementStampingRequestRelatedDocument>();
        foreach (var relatedDocument in document.RelatedDocuments.OrderBy(x => x.Id))
        {
            var enrichment = await BuildRelatedDocumentFiscalSnapshotAsync(relatedDocument, cancellationToken);
            if (!enrichment.IsSuccess)
            {
                return RequestBuildResult.Fail(enrichment.ErrorMessage!);
            }

            relatedDocuments.Add(new PaymentComplementStampingRequestRelatedDocument
            {
                AccountsReceivableInvoiceId = relatedDocument.AccountsReceivableInvoiceId,
                FiscalDocumentId = relatedDocument.FiscalDocumentId,
                RelatedDocumentUuid = relatedDocument.RelatedDocumentUuid,
                InstallmentNumber = relatedDocument.InstallmentNumber,
                PreviousBalance = relatedDocument.PreviousBalance,
                PaidAmount = relatedDocument.PaidAmount,
                RemainingBalance = relatedDocument.RemainingBalance,
                CurrencyCode = relatedDocument.CurrencyCode,
                TaxObjectCode = enrichment.TaxObjectCode,
                TaxTransfers = enrichment.TaxTransfers
            });
        }

        var request = new PaymentComplementStampingRequest
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
            PaymentFormSat = FiscalMasterDataNormalization.NormalizeRequiredCode(payment.PaymentFormSat),
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
            RelatedDocuments = relatedDocuments
        };

        return RequestBuildResult.Success(request);
    }

    private async Task<RelatedDocumentFiscalSnapshot> BuildRelatedDocumentFiscalSnapshotAsync(
        PaymentComplementRelatedDocument relatedDocument,
        CancellationToken cancellationToken)
    {
        if (relatedDocument.PaidAmount <= 0)
        {
            return RelatedDocumentFiscalSnapshot.Fail(
                $"Payment complement related document '{relatedDocument.RelatedDocumentUuid}' must have a paid amount greater than zero.");
        }

        if (relatedDocument.FiscalDocumentId.HasValue)
        {
            var fiscalDocument = await _fiscalDocumentRepository.GetByIdAsync(relatedDocument.FiscalDocumentId.Value, cancellationToken);
            if (fiscalDocument is null)
            {
                return RelatedDocumentFiscalSnapshot.Fail(
                    $"Fiscal document '{relatedDocument.FiscalDocumentId.Value}' was not found for payment complement relation.");
            }

            return BuildFiscalSnapshotFromInternalDocument(fiscalDocument, relatedDocument);
        }

        if (relatedDocument.ExternalRepBaseDocumentId.HasValue)
        {
            var externalDocument = await _externalRepBaseDocumentRepository.GetByIdAsync(relatedDocument.ExternalRepBaseDocumentId.Value, cancellationToken);
            if (externalDocument is null)
            {
                return RelatedDocumentFiscalSnapshot.Fail(
                    $"External REP base document '{relatedDocument.ExternalRepBaseDocumentId.Value}' was not found for payment complement relation.");
            }

            return BuildFiscalSnapshotFromExternalXml(externalDocument, relatedDocument);
        }

        return RelatedDocumentFiscalSnapshot.Fail(
            $"Payment complement related document '{relatedDocument.RelatedDocumentUuid}' is missing its fiscal source.");
    }

    private static RelatedDocumentFiscalSnapshot BuildFiscalSnapshotFromInternalDocument(
        FiscalDocument fiscalDocument,
        PaymentComplementRelatedDocument relatedDocument)
    {
        if (fiscalDocument.Total <= 0)
        {
            return RelatedDocumentFiscalSnapshot.Fail(
                $"Fiscal document '{fiscalDocument.Id}' has an invalid total for REP tax breakdown.");
        }

        var transferGroups = fiscalDocument.Items
            .Where(x => string.Equals(x.TaxObjectCode, "02", StringComparison.OrdinalIgnoreCase))
            .GroupBy(x => new
            {
                TaxCode = DefaultTaxCode,
                FactorType = "Tasa",
                Rate = NormalizeRate(x.VatRate)
            })
            .Select(group => new TaxTransferSource
            {
                TaxCode = group.Key.TaxCode,
                FactorType = group.Key.FactorType,
                Rate = group.Key.Rate,
                BaseAmount = group.Sum(x => x.Subtotal),
                TaxAmount = group.Sum(x => x.TaxTotal)
            })
            .ToList();

        return BuildRelatedDocumentSnapshotFromTransferSources(
            fiscalDocument.Total,
            transferGroups,
            relatedDocument);
    }

    private static RelatedDocumentFiscalSnapshot BuildFiscalSnapshotFromExternalXml(
        ExternalRepBaseDocument externalDocument,
        PaymentComplementRelatedDocument relatedDocument)
    {
        if (string.IsNullOrWhiteSpace(externalDocument.XmlContent))
        {
            return RelatedDocumentFiscalSnapshot.Fail(
                $"External REP base document '{externalDocument.Id}' is missing XML content required for REP tax breakdown.");
        }

        try
        {
            var xml = XDocument.Parse(externalDocument.XmlContent, LoadOptions.PreserveWhitespace);
            var root = xml.Root;
            if (root is null || !string.Equals(root.Name.LocalName, "Comprobante", StringComparison.Ordinal))
            {
                return RelatedDocumentFiscalSnapshot.Fail(
                    $"External REP base document '{externalDocument.Id}' XML does not contain a CFDI Comprobante root.");
            }

            var total = ParseRequiredDecimal(root, "Total");
            var conceptoTraslados = root
                .Descendants()
                .Where(x => string.Equals(x.Name.LocalName, "Traslado", StringComparison.Ordinal))
                .Where(x => x.Parent is not null && string.Equals(x.Parent.Name.LocalName, "Traslados", StringComparison.Ordinal))
                .Select(x => new TaxTransferSource
                {
                    TaxCode = NormalizeOptionalCode(GetAttribute(x, "Impuesto")) ?? DefaultTaxCode,
                    FactorType = NormalizeOptionalCode(GetAttribute(x, "TipoFactor")) ?? "Tasa",
                    Rate = ParseOptionalDecimal(GetAttribute(x, "TasaOCuota")) ?? 0m,
                    BaseAmount = ParseOptionalDecimal(GetAttribute(x, "Base")) ?? 0m,
                    TaxAmount = ParseOptionalDecimal(GetAttribute(x, "Importe")) ?? 0m
                })
                .Where(x => string.Equals(x.TaxCode, DefaultTaxCode, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return BuildRelatedDocumentSnapshotFromTransferSources(total, conceptoTraslados, relatedDocument);
        }
        catch (Exception)
        {
            return RelatedDocumentFiscalSnapshot.Fail(
                $"External REP base document '{externalDocument.Id}' XML could not be parsed for REP tax breakdown.");
        }
    }

    private static RelatedDocumentFiscalSnapshot BuildRelatedDocumentSnapshotFromTransferSources(
        decimal originalDocumentTotal,
        IReadOnlyCollection<TaxTransferSource> transferSources,
        PaymentComplementRelatedDocument relatedDocument)
    {
        if (originalDocumentTotal <= 0)
        {
            return RelatedDocumentFiscalSnapshot.Fail(
                $"Payment complement related document '{relatedDocument.RelatedDocumentUuid}' has an invalid original total.");
        }

        var paidRatio = relatedDocument.PaidAmount / originalDocumentTotal;
        if (paidRatio <= 0)
        {
            return RelatedDocumentFiscalSnapshot.Fail(
                $"Payment complement related document '{relatedDocument.RelatedDocumentUuid}' has an invalid paid ratio.");
        }

        if (transferSources.Count == 0)
        {
            return RelatedDocumentFiscalSnapshot.Success("01", []);
        }

        var paidTransfers = transferSources
            .GroupBy(x => new { x.TaxCode, x.FactorType, x.Rate })
            .Select(group => new PaymentComplementStampingRequestTaxTransfer
            {
                TaxCode = group.Key.TaxCode,
                FactorType = group.Key.FactorType,
                Rate = group.Key.Rate,
                BaseAmount = NormalizeMoney(group.Sum(x => x.BaseAmount) * paidRatio),
                TaxAmount = NormalizeMoney(group.Sum(x => x.TaxAmount) * paidRatio)
            })
            .Where(x => x.BaseAmount > 0 || x.TaxAmount > 0 || x.Rate == 0m)
            .ToList();

        return RelatedDocumentFiscalSnapshot.Success(
            paidTransfers.Count == 0 ? "01" : "02",
            paidTransfers);
    }

    private static decimal NormalizeMoney(decimal amount)
    {
        return Math.Round(amount, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal NormalizeRate(decimal rate)
    {
        return Math.Round(rate, 6, MidpointRounding.AwayFromZero);
    }

    private static decimal ParseRequiredDecimal(XElement element, string attributeName)
    {
        var value = ParseOptionalDecimal(GetAttribute(element, attributeName));
        if (!value.HasValue)
        {
            throw new InvalidOperationException($"Attribute '{attributeName}' is required.");
        }

        return value.Value;
    }

    private static decimal? ParseOptionalDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static string? GetAttribute(XElement element, string localName)
    {
        return element.Attributes()
            .FirstOrDefault(x => string.Equals(x.Name.LocalName, localName, StringComparison.Ordinal))
            ?.Value;
    }

    private static string? NormalizeOptionalCode(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : FiscalMasterDataNormalization.NormalizeRequiredCode(value);
    }

    private sealed class TaxTransferSource
    {
        public string TaxCode { get; init; } = string.Empty;

        public string FactorType { get; init; } = string.Empty;

        public decimal Rate { get; init; }

        public decimal BaseAmount { get; init; }

        public decimal TaxAmount { get; init; }
    }

    private sealed class RelatedDocumentFiscalSnapshot
    {
        public bool IsSuccess { get; init; }

        public string? ErrorMessage { get; init; }

        public string TaxObjectCode { get; init; } = "01";

        public List<PaymentComplementStampingRequestTaxTransfer> TaxTransfers { get; init; } = [];

        public static RelatedDocumentFiscalSnapshot Success(
            string taxObjectCode,
            List<PaymentComplementStampingRequestTaxTransfer> taxTransfers)
        {
            return new RelatedDocumentFiscalSnapshot
            {
                IsSuccess = true,
                TaxObjectCode = taxObjectCode,
                TaxTransfers = taxTransfers
            };
        }

        public static RelatedDocumentFiscalSnapshot Fail(string errorMessage)
        {
            return new RelatedDocumentFiscalSnapshot
            {
                IsSuccess = false,
                ErrorMessage = errorMessage
            };
        }
    }

    private sealed class RequestBuildResult
    {
        public bool IsSuccess { get; init; }

        public string? ErrorMessage { get; init; }

        public PaymentComplementStampingRequest? Request { get; init; }

        public static RequestBuildResult Success(PaymentComplementStampingRequest request)
        {
            return new RequestBuildResult
            {
                IsSuccess = true,
                Request = request
            };
        }

        public static RequestBuildResult Fail(string errorMessage)
        {
            return new RequestBuildResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage
            };
        }
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
