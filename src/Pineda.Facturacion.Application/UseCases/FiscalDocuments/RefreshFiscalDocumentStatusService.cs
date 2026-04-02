using Pineda.Facturacion.Application.Abstractions.Pac;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Application.Contracts.Pac;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public class RefreshFiscalDocumentStatusService
{
    private readonly IFiscalDocumentRepository _fiscalDocumentRepository;
    private readonly IFiscalStampRepository _fiscalStampRepository;
    private readonly IAccountsReceivableInvoiceRepository _accountsReceivableInvoiceRepository;
    private readonly IFiscalStatusQueryGateway _fiscalStatusQueryGateway;
    private readonly IUnitOfWork _unitOfWork;

    public RefreshFiscalDocumentStatusService(
        IFiscalDocumentRepository fiscalDocumentRepository,
        IFiscalStampRepository fiscalStampRepository,
        IAccountsReceivableInvoiceRepository accountsReceivableInvoiceRepository,
        IFiscalStatusQueryGateway fiscalStatusQueryGateway,
        IUnitOfWork unitOfWork)
    {
        _fiscalDocumentRepository = fiscalDocumentRepository;
        _fiscalStampRepository = fiscalStampRepository;
        _accountsReceivableInvoiceRepository = accountsReceivableInvoiceRepository;
        _fiscalStatusQueryGateway = fiscalStatusQueryGateway;
        _unitOfWork = unitOfWork;
    }

    public async Task<RefreshFiscalDocumentStatusResult> ExecuteAsync(
        RefreshFiscalDocumentStatusCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.FiscalDocumentId <= 0)
        {
            return ValidationFailure(command.FiscalDocumentId, "Fiscal document id is required.");
        }

        var fiscalDocument = await _fiscalDocumentRepository.GetTrackedByIdAsync(command.FiscalDocumentId, cancellationToken);
        if (fiscalDocument is null)
        {
            return new RefreshFiscalDocumentStatusResult
            {
                Outcome = RefreshFiscalDocumentStatusOutcome.NotFound,
                IsSuccess = false,
                FiscalDocumentId = command.FiscalDocumentId,
                ErrorMessage = $"Fiscal document '{command.FiscalDocumentId}' was not found."
            };
        }

        var fiscalStamp = await _fiscalStampRepository.GetTrackedByFiscalDocumentIdAsync(command.FiscalDocumentId, cancellationToken);
        if (fiscalStamp is null || string.IsNullOrWhiteSpace(fiscalStamp.Uuid))
        {
            return ValidationFailure(command.FiscalDocumentId, "A stamped fiscal document with UUID evidence is required for status refresh.");
        }

        var request = new FiscalStatusQueryRequest
        {
            FiscalDocumentId = fiscalDocument.Id,
            Uuid = fiscalStamp.Uuid!,
            IssuerRfc = FiscalMasterDataNormalization.NormalizeRequiredCode(fiscalDocument.IssuerRfc),
            ReceiverRfc = FiscalMasterDataNormalization.NormalizeRequiredCode(fiscalDocument.ReceiverRfc),
            Total = fiscalDocument.Total
        };

        FiscalStatusQueryGatewayResult gatewayResult;
        try
        {
            gatewayResult = await _fiscalStatusQueryGateway.QueryStatusAsync(request, cancellationToken);
        }
        catch
        {
            gatewayResult = new FiscalStatusQueryGatewayResult
            {
                Outcome = FiscalStatusQueryGatewayOutcome.Unavailable,
                ProviderName = "FacturaloPlus",
                ProviderOperation = "consultarEstadoSAT",
                CheckedAtUtc = DateTime.UtcNow,
                ErrorMessage = "Provider transport failure.",
                RawResponseSummaryJson = "{\"error\":\"provider_transport_failure\"}"
            };
        }

        if (gatewayResult.Outcome == FiscalStatusQueryGatewayOutcome.ValidationFailed)
        {
            fiscalStamp.LastStatusCheckAtUtc = gatewayResult.CheckedAtUtc == default ? DateTime.UtcNow : gatewayResult.CheckedAtUtc;
            fiscalStamp.LastStatusProviderCode = gatewayResult.ProviderCode;
            fiscalStamp.LastStatusProviderMessage = gatewayResult.ProviderMessage ?? gatewayResult.ErrorMessage;
            fiscalStamp.LastStatusRawResponseSummaryJson = gatewayResult.RawResponseSummaryJson;
            fiscalStamp.UpdatedAtUtc = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return ValidationFailure(
                command.FiscalDocumentId,
                gatewayResult.ErrorMessage ?? "Status refresh request validation failed.",
                fiscalDocument.Status,
                fiscalStamp.Uuid,
                gatewayResult.ProviderCode,
                gatewayResult.ProviderMessage,
                gatewayResult.SupportMessage,
                gatewayResult.RawResponseSummaryJson,
                fiscalStamp.LastStatusCheckAtUtc);
        }

        if (gatewayResult.Outcome == FiscalStatusQueryGatewayOutcome.Unavailable)
        {
            var interpretation = FiscalStatusOperationalInterpreter.BuildUnavailable(gatewayResult.ErrorMessage);
            return new RefreshFiscalDocumentStatusResult
            {
                Outcome = RefreshFiscalDocumentStatusOutcome.ProviderUnavailable,
                IsSuccess = false,
                ErrorMessage = gatewayResult.ErrorMessage ?? "Provider unavailable.",
                FiscalDocumentId = fiscalDocument.Id,
                FiscalDocumentStatus = fiscalDocument.Status,
                Uuid = fiscalStamp.Uuid,
                LastKnownExternalStatus = fiscalStamp.LastKnownExternalStatus,
                ProviderCode = fiscalStamp.LastStatusProviderCode,
                ProviderMessage = fiscalStamp.LastStatusProviderMessage,
                OperationalStatus = interpretation.Status.ToString(),
                OperationalMessage = interpretation.UserMessage,
                SupportMessage = interpretation.SupportMessage,
                RawResponseSummaryJson = fiscalStamp.LastStatusRawResponseSummaryJson,
                CheckedAtUtc = fiscalStamp.LastStatusCheckAtUtc
            };
        }

        var operationalInterpretation = FiscalStatusOperationalInterpreter.Interpret(
            gatewayResult.ProviderCode,
            gatewayResult.ExternalStatus,
            gatewayResult.Cancelability,
            gatewayResult.CancellationStatus);

        fiscalStamp.LastStatusCheckAtUtc = gatewayResult.CheckedAtUtc;
        fiscalStamp.LastKnownExternalStatus = gatewayResult.ExternalStatus;
        fiscalStamp.LastStatusProviderCode = gatewayResult.ProviderCode;
        fiscalStamp.LastStatusProviderMessage = gatewayResult.ProviderMessage;
        fiscalStamp.LastStatusRawResponseSummaryJson = gatewayResult.RawResponseSummaryJson;
        fiscalStamp.UpdatedAtUtc = DateTime.UtcNow;

        AlignFiscalDocumentStatus(fiscalDocument, gatewayResult.ExternalStatus, gatewayResult.CancellationStatus);
        fiscalDocument.UpdatedAtUtc = DateTime.UtcNow;

        if (fiscalDocument.Status == FiscalDocumentStatus.Cancelled)
        {
            await CancelAccountsReceivableInvoiceIfPresentAsync(fiscalDocument.Id, fiscalDocument.UpdatedAtUtc, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new RefreshFiscalDocumentStatusResult
        {
            Outcome = RefreshFiscalDocumentStatusOutcome.Refreshed,
            IsSuccess = true,
            FiscalDocumentId = fiscalDocument.Id,
            FiscalDocumentStatus = fiscalDocument.Status,
            Uuid = fiscalStamp.Uuid,
            LastKnownExternalStatus = fiscalStamp.LastKnownExternalStatus,
            ProviderCode = fiscalStamp.LastStatusProviderCode,
            ProviderMessage = fiscalStamp.LastStatusProviderMessage,
            OperationalStatus = operationalInterpretation.Status.ToString(),
            OperationalMessage = operationalInterpretation.UserMessage,
            SupportMessage = operationalInterpretation.SupportMessage,
            RawResponseSummaryJson = fiscalStamp.LastStatusRawResponseSummaryJson,
            CheckedAtUtc = fiscalStamp.LastStatusCheckAtUtc
        };
    }

    private async Task CancelAccountsReceivableInvoiceIfPresentAsync(long fiscalDocumentId, DateTime now, CancellationToken cancellationToken)
    {
        var accountsReceivableInvoice = await _accountsReceivableInvoiceRepository.GetTrackedByFiscalDocumentIdAsync(fiscalDocumentId, cancellationToken);
        if (accountsReceivableInvoice is null || accountsReceivableInvoice.Status == AccountsReceivableInvoiceStatus.Cancelled)
        {
            return;
        }

        accountsReceivableInvoice.Status = AccountsReceivableInvoiceStatus.Cancelled;
        accountsReceivableInvoice.UpdatedAtUtc = now;
    }

    private static void AlignFiscalDocumentStatus(
        Domain.Entities.FiscalDocument fiscalDocument,
        string? externalStatus,
        string? cancellationStatus)
    {
        var normalizedStatus = FiscalMasterDataNormalization.NormalizeOptionalText(externalStatus)?.ToUpperInvariant();
        var normalizedCancellationStatus = FiscalMasterDataNormalization.NormalizeOptionalText(cancellationStatus)?.ToUpperInvariant();

        if (normalizedStatus is "CANCELLED" or "CANCELED")
        {
            fiscalDocument.Status = FiscalDocumentStatus.Cancelled;
            return;
        }

        if (normalizedStatus == "CANCELADO")
        {
            fiscalDocument.Status = FiscalDocumentStatus.Cancelled;
            return;
        }

        if (normalizedCancellationStatus == "EN PROCESO")
        {
            fiscalDocument.Status = FiscalDocumentStatus.CancellationRequested;
            return;
        }

        if (normalizedCancellationStatus == "SOLICITUD RECHAZADA")
        {
            fiscalDocument.Status = FiscalDocumentStatus.CancellationRejected;
            return;
        }

        if (normalizedStatus is "STAMPED" or "ACTIVE" or "VIGENTE")
        {
            if (fiscalDocument.Status == FiscalDocumentStatus.CancellationRequested
                || fiscalDocument.Status == FiscalDocumentStatus.CancellationRejected)
            {
                fiscalDocument.Status = FiscalDocumentStatus.Stamped;
            }
        }
    }

    private static RefreshFiscalDocumentStatusResult ValidationFailure(
        long fiscalDocumentId,
        string errorMessage,
        FiscalDocumentStatus? fiscalDocumentStatus = null,
        string? uuid = null,
        string? providerCode = null,
        string? providerMessage = null,
        string? supportMessage = null,
        string? rawResponseSummaryJson = null,
        DateTime? checkedAtUtc = null)
    {
        return new RefreshFiscalDocumentStatusResult
        {
            Outcome = RefreshFiscalDocumentStatusOutcome.ValidationFailed,
            IsSuccess = false,
            FiscalDocumentId = fiscalDocumentId,
            FiscalDocumentStatus = fiscalDocumentStatus,
            Uuid = uuid,
            ProviderCode = providerCode,
            ProviderMessage = providerMessage,
            SupportMessage = supportMessage,
            RawResponseSummaryJson = rawResponseSummaryJson,
            CheckedAtUtc = checkedAtUtc,
            ErrorMessage = errorMessage
        };
    }
}
