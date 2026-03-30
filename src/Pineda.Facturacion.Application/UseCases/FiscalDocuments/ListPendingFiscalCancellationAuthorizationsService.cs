using Pineda.Facturacion.Application.Abstractions.Pac;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Application.Contracts.Pac;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public class ListPendingFiscalCancellationAuthorizationsService
{
    private readonly IIssuerProfileRepository _issuerProfileRepository;
    private readonly IFiscalStampRepository _fiscalStampRepository;
    private readonly IFiscalDocumentRepository _fiscalDocumentRepository;
    private readonly IFiscalCancellationRepository _fiscalCancellationRepository;
    private readonly IFiscalCancellationGateway _fiscalCancellationGateway;

    public ListPendingFiscalCancellationAuthorizationsService(
        IIssuerProfileRepository issuerProfileRepository,
        IFiscalStampRepository fiscalStampRepository,
        IFiscalDocumentRepository fiscalDocumentRepository,
        IFiscalCancellationRepository fiscalCancellationRepository,
        IFiscalCancellationGateway fiscalCancellationGateway)
    {
        _issuerProfileRepository = issuerProfileRepository;
        _fiscalStampRepository = fiscalStampRepository;
        _fiscalDocumentRepository = fiscalDocumentRepository;
        _fiscalCancellationRepository = fiscalCancellationRepository;
        _fiscalCancellationGateway = fiscalCancellationGateway;
    }

    public async Task<ListPendingFiscalCancellationAuthorizationsResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var issuerProfile = await _issuerProfileRepository.GetActiveAsync(cancellationToken);
        if (issuerProfile is null)
        {
            return ValidationFailure("An active issuer profile is required to consult pending cancellation authorizations.");
        }

        if (string.IsNullOrWhiteSpace(issuerProfile.CertificateReference))
        {
            return ValidationFailure("Active issuer certificate reference is required to consult pending cancellation authorizations.");
        }

        if (string.IsNullOrWhiteSpace(issuerProfile.PrivateKeyReference))
        {
            return ValidationFailure("Active issuer private key reference is required to consult pending cancellation authorizations.");
        }

        var gatewayResult = await _fiscalCancellationGateway.ListPendingAuthorizationsAsync(
            new FiscalCancellationAuthorizationPendingQueryRequest
            {
                CertificateReference = FiscalMasterDataNormalization.NormalizeRequiredText(issuerProfile.CertificateReference),
                PrivateKeyReference = FiscalMasterDataNormalization.NormalizeRequiredText(issuerProfile.PrivateKeyReference)
            },
            cancellationToken);

        if (gatewayResult.Outcome == FiscalCancellationAuthorizationPendingQueryGatewayOutcome.ValidationFailed)
        {
            return new ListPendingFiscalCancellationAuthorizationsResult
            {
                Outcome = ListPendingFiscalCancellationAuthorizationsOutcome.ValidationFailed,
                IsSuccess = false,
                ErrorMessage = gatewayResult.ErrorMessage,
                ProviderName = gatewayResult.ProviderName,
                ProviderCode = gatewayResult.ProviderCode,
                ProviderMessage = gatewayResult.ProviderMessage,
                SupportMessage = gatewayResult.SupportMessage,
                RawResponseSummaryJson = gatewayResult.RawResponseSummaryJson
            };
        }

        if (gatewayResult.Outcome == FiscalCancellationAuthorizationPendingQueryGatewayOutcome.Unavailable)
        {
            return new ListPendingFiscalCancellationAuthorizationsResult
            {
                Outcome = ListPendingFiscalCancellationAuthorizationsOutcome.ProviderUnavailable,
                IsSuccess = false,
                ErrorMessage = gatewayResult.ErrorMessage,
                ProviderName = gatewayResult.ProviderName,
                ProviderCode = gatewayResult.ProviderCode,
                ProviderMessage = gatewayResult.ProviderMessage,
                SupportMessage = gatewayResult.SupportMessage,
                RawResponseSummaryJson = gatewayResult.RawResponseSummaryJson
            };
        }

        var items = new List<PendingFiscalCancellationAuthorizationItem>();
        foreach (var item in gatewayResult.Items)
        {
            var pendingItem = new PendingFiscalCancellationAuthorizationItem
            {
                Uuid = item.Uuid,
                IssuerRfc = item.IssuerRfc,
                ReceiverRfc = item.ReceiverRfc,
                ProviderCode = item.ProviderCode,
                ProviderMessage = item.ProviderMessage,
                RequestedAtUtc = item.RequestedAtUtc,
                RawItemSummaryJson = item.RawItemSummaryJson
            };

            if (!string.IsNullOrWhiteSpace(item.Uuid))
            {
                var fiscalStamp = await _fiscalStampRepository.GetByUuidAsync(item.Uuid, cancellationToken);
                if (fiscalStamp is not null)
                {
                    pendingItem.FiscalDocumentId = fiscalStamp.FiscalDocumentId;

                    var fiscalDocument = await _fiscalDocumentRepository.GetByIdAsync(fiscalStamp.FiscalDocumentId, cancellationToken);
                    if (fiscalDocument is not null)
                    {
                        pendingItem.FiscalDocumentStatus = fiscalDocument.Status.ToString();
                        pendingItem.LocalOperationalStatus = MapLocalOperationalStatus(fiscalDocument.Status);
                        pendingItem.LocalOperationalMessage = BuildLocalOperationalMessage(fiscalDocument.Status);
                    }

                    var fiscalCancellation = await _fiscalCancellationRepository.GetByFiscalDocumentIdAsync(fiscalStamp.FiscalDocumentId, cancellationToken);
                    if (fiscalCancellation is not null)
                    {
                        pendingItem.FiscalCancellationId = fiscalCancellation.Id;
                        pendingItem.CancellationStatus = fiscalCancellation.Status.ToString();
                        pendingItem.AuthorizationStatus = FiscalCancellationAuthorizationStatus.Pending.ToString();
                    }
                }
            }

            items.Add(pendingItem);
        }

        return new ListPendingFiscalCancellationAuthorizationsResult
        {
            Outcome = ListPendingFiscalCancellationAuthorizationsOutcome.Retrieved,
            IsSuccess = true,
            ProviderName = gatewayResult.ProviderName,
            ProviderCode = gatewayResult.ProviderCode,
            ProviderMessage = gatewayResult.ProviderMessage,
            SupportMessage = gatewayResult.SupportMessage,
            RawResponseSummaryJson = gatewayResult.RawResponseSummaryJson,
            Items = items
        };
    }

    private static ListPendingFiscalCancellationAuthorizationsResult ValidationFailure(string errorMessage)
    {
        return new ListPendingFiscalCancellationAuthorizationsResult
        {
            Outcome = ListPendingFiscalCancellationAuthorizationsOutcome.ValidationFailed,
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }

    private static string MapLocalOperationalStatus(FiscalDocumentStatus status)
    {
        return status switch
        {
            FiscalDocumentStatus.Cancelled => FiscalOperationalStatus.Cancelled.ToString(),
            FiscalDocumentStatus.CancellationRejected => FiscalOperationalStatus.CancellationRejected.ToString(),
            FiscalDocumentStatus.CancellationRequested => FiscalOperationalStatus.CancellationPending.ToString(),
            _ => FiscalOperationalStatus.Active.ToString()
        };
    }

    private static string BuildLocalOperationalMessage(FiscalDocumentStatus status)
    {
        return status switch
        {
            FiscalDocumentStatus.Cancelled => "Documento cancelado localmente.",
            FiscalDocumentStatus.CancellationRejected => "La última solicitud de cancelación fue rechazada.",
            FiscalDocumentStatus.CancellationRequested => "La cancelación sigue pendiente de confirmación.",
            _ => "Documento vigente en el flujo local."
        };
    }
}
