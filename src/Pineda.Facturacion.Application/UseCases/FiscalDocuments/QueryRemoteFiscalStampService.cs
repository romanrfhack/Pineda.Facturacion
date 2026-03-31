using Pineda.Facturacion.Application.Abstractions.Pac;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Application.Contracts.Pac;

namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public class QueryRemoteFiscalStampService
{
    private readonly IFiscalDocumentRepository _fiscalDocumentRepository;
    private readonly IFiscalStampRepository _fiscalStampRepository;
    private readonly IFiscalStampingGateway _fiscalStampingGateway;
    private readonly IUnitOfWork _unitOfWork;

    public QueryRemoteFiscalStampService(
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

    public async Task<QueryRemoteFiscalStampResult> ExecuteAsync(
        QueryRemoteFiscalStampCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.FiscalDocumentId <= 0)
        {
            return ValidationFailure(command.FiscalDocumentId, "Fiscal document id is required.");
        }

        var fiscalDocument = await _fiscalDocumentRepository.GetTrackedByIdAsync(command.FiscalDocumentId, cancellationToken);
        if (fiscalDocument is null)
        {
            return new QueryRemoteFiscalStampResult
            {
                Outcome = QueryRemoteFiscalStampOutcome.NotFound,
                IsSuccess = false,
                FiscalDocumentId = command.FiscalDocumentId,
                ErrorMessage = $"Fiscal document '{command.FiscalDocumentId}' was not found."
            };
        }

        var fiscalStamp = await _fiscalStampRepository.GetTrackedByFiscalDocumentIdAsync(command.FiscalDocumentId, cancellationToken);
        if (fiscalStamp is null || string.IsNullOrWhiteSpace(fiscalStamp.Uuid))
        {
            return ValidationFailure(
                fiscalDocument.Id,
                "A stamped fiscal document with UUID evidence is required for remote PAC lookup.",
                fiscalDocument.Status,
                fiscalStamp?.Id,
                fiscalStamp?.Uuid,
                hasLocalXml: !string.IsNullOrWhiteSpace(fiscalStamp?.XmlContent));
        }

        var hasLocalXml = !string.IsNullOrWhiteSpace(fiscalStamp.XmlContent);
        FiscalRemoteCfdiQueryGatewayResult gatewayResult;

        try
        {
            gatewayResult = await _fiscalStampingGateway.QueryRemoteCfdiAsync(
                new FiscalRemoteCfdiQueryRequest
                {
                    FiscalDocumentId = fiscalDocument.Id,
                    Uuid = fiscalStamp.Uuid!
                },
                cancellationToken);
        }
        catch
        {
            gatewayResult = new FiscalRemoteCfdiQueryGatewayResult
            {
                Outcome = FiscalRemoteCfdiQueryGatewayOutcome.Unavailable,
                ProviderName = "FacturaloPlus",
                ProviderOperation = "consultarCFDI",
                ErrorMessage = "Provider transport failure.",
                SupportMessage = "La consulta remota del CFDI falló por transporte. Puedes reintentar manualmente.",
                RawResponseSummaryJson = "{\"error\":\"provider_transport_failure\"}"
            };
        }

        var now = DateTime.UtcNow;
        fiscalStamp.LastRemoteQueryAtUtc = now;
        fiscalStamp.LastRemoteProviderTrackingId = gatewayResult.ProviderTrackingId;
        fiscalStamp.LastRemoteProviderCode = gatewayResult.ProviderCode;
        fiscalStamp.LastRemoteProviderMessage = gatewayResult.ProviderMessage ?? gatewayResult.ErrorMessage;
        fiscalStamp.LastRemoteRawResponseSummaryJson = gatewayResult.RawResponseSummaryJson;

        var xmlRecoveredLocally = false;
        if (!hasLocalXml && gatewayResult.RemoteExists && !string.IsNullOrWhiteSpace(gatewayResult.XmlContent))
        {
            fiscalStamp.XmlContent = gatewayResult.XmlContent;
            fiscalStamp.XmlHash = gatewayResult.XmlHash ?? ComputeSha256(gatewayResult.XmlContent);
            fiscalStamp.XmlRecoveredFromProviderAtUtc = now;
            xmlRecoveredLocally = true;
        }

        fiscalStamp.UpdatedAtUtc = now;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var result = new QueryRemoteFiscalStampResult
        {
            FiscalDocumentId = fiscalDocument.Id,
            FiscalDocumentStatus = fiscalDocument.Status,
            FiscalStampId = fiscalStamp.Id,
            Uuid = fiscalStamp.Uuid,
            HasLocalXml = !string.IsNullOrWhiteSpace(fiscalStamp.XmlContent),
            RemoteExists = gatewayResult.RemoteExists,
            HasRemoteXml = !string.IsNullOrWhiteSpace(gatewayResult.XmlContent),
            XmlRecoveredLocally = xmlRecoveredLocally,
            ProviderName = gatewayResult.ProviderName,
            ProviderOperation = gatewayResult.ProviderOperation,
            ProviderTrackingId = gatewayResult.ProviderTrackingId,
            ProviderCode = gatewayResult.ProviderCode,
            ProviderMessage = gatewayResult.ProviderMessage,
            ErrorCode = gatewayResult.ErrorCode,
            SupportMessage = BuildSupportMessage(hasLocalXml, gatewayResult.RemoteExists, xmlRecoveredLocally, gatewayResult.SupportMessage, gatewayResult.ProviderMessage),
            RawResponseSummaryJson = gatewayResult.RawResponseSummaryJson,
            CheckedAtUtc = fiscalStamp.LastRemoteQueryAtUtc
        };

        switch (gatewayResult.Outcome)
        {
            case FiscalRemoteCfdiQueryGatewayOutcome.Found:
                result.Outcome = QueryRemoteFiscalStampOutcome.FoundRemote;
                result.IsSuccess = true;
                return result;
            case FiscalRemoteCfdiQueryGatewayOutcome.NotFound:
                result.Outcome = QueryRemoteFiscalStampOutcome.NotFound;
                result.IsSuccess = false;
                result.ErrorMessage = "PAC did not return remote CFDI evidence for the provided UUID.";
                return result;
            case FiscalRemoteCfdiQueryGatewayOutcome.ValidationFailed:
                result.Outcome = QueryRemoteFiscalStampOutcome.ValidationFailed;
                result.IsSuccess = false;
                result.ErrorMessage = gatewayResult.ErrorMessage ?? "Remote CFDI query could not be validated.";
                return result;
            default:
                result.Outcome = QueryRemoteFiscalStampOutcome.ProviderUnavailable;
                result.IsSuccess = false;
                result.ErrorMessage = gatewayResult.ErrorMessage ?? "Provider unavailable.";
                return result;
        }
    }

    private static QueryRemoteFiscalStampResult ValidationFailure(
        long fiscalDocumentId,
        string errorMessage,
        Domain.Enums.FiscalDocumentStatus? fiscalDocumentStatus = null,
        long? fiscalStampId = null,
        string? uuid = null,
        bool hasLocalXml = false)
    {
        return new QueryRemoteFiscalStampResult
        {
            Outcome = QueryRemoteFiscalStampOutcome.ValidationFailed,
            IsSuccess = false,
            FiscalDocumentId = fiscalDocumentId,
            FiscalDocumentStatus = fiscalDocumentStatus,
            FiscalStampId = fiscalStampId,
            Uuid = uuid,
            HasLocalXml = hasLocalXml,
            ErrorMessage = errorMessage
        };
    }

    private static string BuildSupportMessage(
        bool hadLocalXmlBeforeQuery,
        bool remoteExists,
        bool xmlRecoveredLocally,
        string? gatewaySupportMessage,
        string? providerMessage)
    {
        if (!string.IsNullOrWhiteSpace(gatewaySupportMessage))
        {
            return gatewaySupportMessage;
        }

        if (remoteExists && hadLocalXmlBeforeQuery)
        {
            return "El CFDI existe localmente y también fue encontrado en el PAC.";
        }

        if (remoteExists && xmlRecoveredLocally)
        {
            return "El CFDI existe en el PAC y el XML faltante se recuperó en la evidencia local.";
        }

        if (remoteExists)
        {
            return "El CFDI fue encontrado en el PAC; revisa el hallazgo remoto para conciliación manual.";
        }

        return providerMessage ?? "El PAC no reportó evidencia concluyente para este UUID.";
    }

    private static string ComputeSha256(string value)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
    }
}
