using Microsoft.AspNetCore.Http.HttpResults;
using Pineda.Facturacion.Api.Security;
using Pineda.Facturacion.Application.Abstractions.Security;
using Pineda.Facturacion.Application.Security;
using Pineda.Facturacion.Application.UseCases.FiscalDocuments;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Api.Endpoints;

public static class FiscalDocumentsEndpoints
{
    public static IEndpointRouteBuilder MapFiscalDocumentsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/fiscal-documents")
            .WithTags("FiscalDocuments")
            .RequireAuthorization(AuthorizationPolicyNames.Authenticated);

        group.MapGet("/{fiscalDocumentId:long}", GetFiscalDocumentByIdAsync)
            .WithName("GetFiscalDocumentById")
            .WithSummary("Get a persisted fiscal document snapshot")
            .Produces<FiscalDocumentResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{fiscalDocumentId:long}/stamp", StampFiscalDocumentAsync)
            .RequireAuthorization(AuthorizationPolicyNames.SupervisorOrAdmin)
            .WithName("StampFiscalDocument")
            .WithSummary("Stamp a persisted fiscal document snapshot")
            .Produces<StampFiscalDocumentResponse>(StatusCodes.Status200OK)
            .Produces<StampFiscalDocumentResponse>(StatusCodes.Status400BadRequest)
            .Produces<StampFiscalDocumentResponse>(StatusCodes.Status404NotFound)
            .Produces<StampFiscalDocumentResponse>(StatusCodes.Status409Conflict)
            .Produces<StampFiscalDocumentResponse>(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/{fiscalDocumentId:long}/stamp", GetFiscalStampByFiscalDocumentIdAsync)
            .WithName("GetFiscalStampByFiscalDocumentId")
            .WithSummary("Get persisted stamp evidence for a fiscal document")
            .Produces<FiscalStampResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{fiscalDocumentId:long}/cancel", CancelFiscalDocumentAsync)
            .RequireAuthorization(AuthorizationPolicyNames.SupervisorOrAdmin)
            .WithName("CancelFiscalDocument")
            .WithSummary("Cancel a stamped fiscal document")
            .Produces<CancelFiscalDocumentResponse>(StatusCodes.Status200OK)
            .Produces<CancelFiscalDocumentResponse>(StatusCodes.Status400BadRequest)
            .Produces<CancelFiscalDocumentResponse>(StatusCodes.Status404NotFound)
            .Produces<CancelFiscalDocumentResponse>(StatusCodes.Status409Conflict)
            .Produces<CancelFiscalDocumentResponse>(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/{fiscalDocumentId:long}/cancellation", GetFiscalCancellationByFiscalDocumentIdAsync)
            .WithName("GetFiscalCancellationByFiscalDocumentId")
            .WithSummary("Get persisted cancellation evidence for a fiscal document")
            .Produces<FiscalCancellationResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{fiscalDocumentId:long}/refresh-status", RefreshFiscalDocumentStatusAsync)
            .RequireAuthorization(AuthorizationPolicyNames.SupervisorOrAdmin)
            .WithName("RefreshFiscalDocumentStatus")
            .WithSummary("Refresh the latest external fiscal status from the PAC/provider")
            .Produces<RefreshFiscalDocumentStatusResponse>(StatusCodes.Status200OK)
            .Produces<RefreshFiscalDocumentStatusResponse>(StatusCodes.Status400BadRequest)
            .Produces<RefreshFiscalDocumentStatusResponse>(StatusCodes.Status404NotFound)
            .Produces<RefreshFiscalDocumentStatusResponse>(StatusCodes.Status503ServiceUnavailable);

        return endpoints;
    }

    private static async Task<Results<Ok<FiscalDocumentResponse>, NotFound>> GetFiscalDocumentByIdAsync(
        long fiscalDocumentId,
        GetFiscalDocumentByIdService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(fiscalDocumentId, cancellationToken);
        if (result.Outcome == GetFiscalDocumentByIdOutcome.NotFound || result.FiscalDocument is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(MapFiscalDocument(result.FiscalDocument));
    }

    private static async Task<Results<Ok<StampFiscalDocumentResponse>, BadRequest<StampFiscalDocumentResponse>, NotFound<StampFiscalDocumentResponse>, Conflict<StampFiscalDocumentResponse>, JsonHttpResult<StampFiscalDocumentResponse>>> StampFiscalDocumentAsync(
        long fiscalDocumentId,
        StampFiscalDocumentRequest? request,
        StampFiscalDocumentService service,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(
            new StampFiscalDocumentCommand
            {
                FiscalDocumentId = fiscalDocumentId,
                RetryRejected = request?.RetryRejected ?? false
            },
            cancellationToken);

        var response = new StampFiscalDocumentResponse
        {
            Outcome = result.Outcome.ToString(),
            IsSuccess = result.IsSuccess,
            ErrorMessage = result.ErrorMessage,
            FiscalDocumentId = result.FiscalDocumentId,
            FiscalDocumentStatus = result.FiscalDocumentStatus?.ToString(),
            FiscalStampId = result.FiscalStampId,
            Uuid = result.Uuid,
            StampedAtUtc = result.StampedAtUtc,
            ProviderName = result.ProviderName,
            ProviderTrackingId = result.ProviderTrackingId
        };

        await AuditApiHelper.RecordAsync(
            auditService,
            "FiscalDocument.Stamp",
            "FiscalDocument",
            fiscalDocumentId.ToString(),
            result.Outcome.ToString(),
            new { fiscalDocumentId, retryRejected = request?.RetryRejected ?? false },
            new { result.FiscalStampId, result.Uuid, result.FiscalDocumentStatus, result.ProviderName, result.ProviderTrackingId },
            result.ErrorMessage,
            cancellationToken);

        return result.Outcome switch
        {
            StampFiscalDocumentOutcome.Stamped => TypedResults.Ok(response),
            StampFiscalDocumentOutcome.NotFound => TypedResults.NotFound(response),
            StampFiscalDocumentOutcome.Conflict => TypedResults.Conflict(response),
            StampFiscalDocumentOutcome.ProviderRejected => TypedResults.Conflict(response),
            StampFiscalDocumentOutcome.ProviderUnavailable => TypedResults.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable),
            StampFiscalDocumentOutcome.ValidationFailed => TypedResults.BadRequest(response),
            _ => TypedResults.BadRequest(response)
        };
    }

    private static async Task<Results<Ok<FiscalStampResponse>, NotFound>> GetFiscalStampByFiscalDocumentIdAsync(
        long fiscalDocumentId,
        GetFiscalStampByFiscalDocumentIdService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(fiscalDocumentId, cancellationToken);
        if (result.Outcome == GetFiscalStampByFiscalDocumentIdOutcome.NotFound || result.FiscalStamp is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(MapFiscalStamp(result.FiscalStamp));
    }

    private static async Task<Results<Ok<CancelFiscalDocumentResponse>, BadRequest<CancelFiscalDocumentResponse>, NotFound<CancelFiscalDocumentResponse>, Conflict<CancelFiscalDocumentResponse>, JsonHttpResult<CancelFiscalDocumentResponse>>> CancelFiscalDocumentAsync(
        long fiscalDocumentId,
        CancelFiscalDocumentRequest request,
        CancelFiscalDocumentService service,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(
            new CancelFiscalDocumentCommand
            {
                FiscalDocumentId = fiscalDocumentId,
                CancellationReasonCode = request.CancellationReasonCode,
                ReplacementUuid = request.ReplacementUuid
            },
            cancellationToken);

        var response = new CancelFiscalDocumentResponse
        {
            Outcome = result.Outcome.ToString(),
            IsSuccess = result.IsSuccess,
            ErrorMessage = result.ErrorMessage,
            FiscalDocumentId = result.FiscalDocumentId,
            FiscalDocumentStatus = result.FiscalDocumentStatus?.ToString(),
            FiscalCancellationId = result.FiscalCancellationId,
            CancellationStatus = result.CancellationStatus?.ToString(),
            ProviderName = result.ProviderName,
            ProviderTrackingId = result.ProviderTrackingId,
            CancelledAtUtc = result.CancelledAtUtc
        };

        await AuditApiHelper.RecordAsync(
            auditService,
            "FiscalDocument.Cancel",
            "FiscalDocument",
            fiscalDocumentId.ToString(),
            result.Outcome.ToString(),
            new { fiscalDocumentId, request.CancellationReasonCode, request.ReplacementUuid },
            new { result.FiscalCancellationId, result.CancellationStatus, result.FiscalDocumentStatus, result.ProviderName, result.ProviderTrackingId },
            result.ErrorMessage,
            cancellationToken);

        return result.Outcome switch
        {
            CancelFiscalDocumentOutcome.Cancelled => TypedResults.Ok(response),
            CancelFiscalDocumentOutcome.NotFound => TypedResults.NotFound(response),
            CancelFiscalDocumentOutcome.Conflict => TypedResults.Conflict(response),
            CancelFiscalDocumentOutcome.ProviderRejected => TypedResults.Conflict(response),
            CancelFiscalDocumentOutcome.ProviderUnavailable => TypedResults.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable),
            CancelFiscalDocumentOutcome.ValidationFailed => TypedResults.BadRequest(response),
            _ => TypedResults.BadRequest(response)
        };
    }

    private static async Task<Results<Ok<FiscalCancellationResponse>, NotFound>> GetFiscalCancellationByFiscalDocumentIdAsync(
        long fiscalDocumentId,
        GetFiscalCancellationByFiscalDocumentIdService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(fiscalDocumentId, cancellationToken);
        if (result.Outcome == GetFiscalCancellationByFiscalDocumentIdOutcome.NotFound || result.FiscalCancellation is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(MapFiscalCancellation(result.FiscalCancellation));
    }

    private static async Task<Results<Ok<RefreshFiscalDocumentStatusResponse>, BadRequest<RefreshFiscalDocumentStatusResponse>, NotFound<RefreshFiscalDocumentStatusResponse>, JsonHttpResult<RefreshFiscalDocumentStatusResponse>>> RefreshFiscalDocumentStatusAsync(
        long fiscalDocumentId,
        RefreshFiscalDocumentStatusService service,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(
            new RefreshFiscalDocumentStatusCommand
            {
                FiscalDocumentId = fiscalDocumentId
            },
            cancellationToken);

        var response = new RefreshFiscalDocumentStatusResponse
        {
            Outcome = result.Outcome.ToString(),
            IsSuccess = result.IsSuccess,
            ErrorMessage = result.ErrorMessage,
            FiscalDocumentId = result.FiscalDocumentId,
            FiscalDocumentStatus = result.FiscalDocumentStatus?.ToString(),
            Uuid = result.Uuid,
            LastKnownExternalStatus = result.LastKnownExternalStatus,
            ProviderCode = result.ProviderCode,
            ProviderMessage = result.ProviderMessage,
            CheckedAtUtc = result.CheckedAtUtc
        };

        await AuditApiHelper.RecordAsync(
            auditService,
            "FiscalDocument.RefreshStatus",
            "FiscalDocument",
            fiscalDocumentId.ToString(),
            result.Outcome.ToString(),
            new { fiscalDocumentId },
            new { result.Uuid, result.LastKnownExternalStatus, result.ProviderCode, result.ProviderMessage, result.CheckedAtUtc, result.FiscalDocumentStatus },
            result.ErrorMessage,
            cancellationToken);

        return result.Outcome switch
        {
            RefreshFiscalDocumentStatusOutcome.Refreshed => TypedResults.Ok(response),
            RefreshFiscalDocumentStatusOutcome.NotFound => TypedResults.NotFound(response),
            RefreshFiscalDocumentStatusOutcome.ProviderUnavailable => TypedResults.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable),
            RefreshFiscalDocumentStatusOutcome.ValidationFailed => TypedResults.BadRequest(response),
            _ => TypedResults.BadRequest(response)
        };
    }

    private static FiscalDocumentResponse MapFiscalDocument(FiscalDocument fiscalDocument)
    {
        return new FiscalDocumentResponse
        {
            Id = fiscalDocument.Id,
            BillingDocumentId = fiscalDocument.BillingDocumentId,
            IssuerProfileId = fiscalDocument.IssuerProfileId,
            FiscalReceiverId = fiscalDocument.FiscalReceiverId,
            Status = fiscalDocument.Status.ToString(),
            CfdiVersion = fiscalDocument.CfdiVersion,
            DocumentType = fiscalDocument.DocumentType,
            Series = fiscalDocument.Series,
            Folio = fiscalDocument.Folio,
            IssuedAtUtc = fiscalDocument.IssuedAtUtc,
            CurrencyCode = fiscalDocument.CurrencyCode,
            ExchangeRate = fiscalDocument.ExchangeRate,
            PaymentMethodSat = fiscalDocument.PaymentMethodSat,
            PaymentFormSat = fiscalDocument.PaymentFormSat,
            PaymentCondition = fiscalDocument.PaymentCondition,
            IsCreditSale = fiscalDocument.IsCreditSale,
            CreditDays = fiscalDocument.CreditDays,
            IssuerRfc = fiscalDocument.IssuerRfc,
            IssuerLegalName = fiscalDocument.IssuerLegalName,
            IssuerFiscalRegimeCode = fiscalDocument.IssuerFiscalRegimeCode,
            IssuerPostalCode = fiscalDocument.IssuerPostalCode,
            PacEnvironment = fiscalDocument.PacEnvironment,
            HasCertificateReference = !string.IsNullOrWhiteSpace(fiscalDocument.CertificateReference),
            HasPrivateKeyReference = !string.IsNullOrWhiteSpace(fiscalDocument.PrivateKeyReference),
            HasPrivateKeyPasswordReference = !string.IsNullOrWhiteSpace(fiscalDocument.PrivateKeyPasswordReference),
            ReceiverRfc = fiscalDocument.ReceiverRfc,
            ReceiverLegalName = fiscalDocument.ReceiverLegalName,
            ReceiverFiscalRegimeCode = fiscalDocument.ReceiverFiscalRegimeCode,
            ReceiverCfdiUseCode = fiscalDocument.ReceiverCfdiUseCode,
            ReceiverPostalCode = fiscalDocument.ReceiverPostalCode,
            ReceiverCountryCode = fiscalDocument.ReceiverCountryCode,
            ReceiverForeignTaxRegistration = fiscalDocument.ReceiverForeignTaxRegistration,
            Subtotal = fiscalDocument.Subtotal,
            DiscountTotal = fiscalDocument.DiscountTotal,
            TaxTotal = fiscalDocument.TaxTotal,
            Total = fiscalDocument.Total,
            CreatedAtUtc = fiscalDocument.CreatedAtUtc,
            UpdatedAtUtc = fiscalDocument.UpdatedAtUtc,
            Items = fiscalDocument.Items
                .OrderBy(x => x.LineNumber)
                .Select(MapItem)
                .ToList()
        };
    }

    private static FiscalStampResponse MapFiscalStamp(FiscalStamp fiscalStamp)
    {
        return new FiscalStampResponse
        {
            FiscalDocumentId = fiscalStamp.FiscalDocumentId,
            ProviderName = fiscalStamp.ProviderName,
            Status = fiscalStamp.Status.ToString(),
            Uuid = fiscalStamp.Uuid,
            StampedAtUtc = fiscalStamp.StampedAtUtc,
            ProviderCode = fiscalStamp.ProviderCode,
            ProviderMessage = fiscalStamp.ProviderMessage,
            ErrorCode = fiscalStamp.ErrorCode,
            ErrorMessage = fiscalStamp.ErrorMessage,
            XmlHash = fiscalStamp.XmlHash,
            QrCodeTextOrUrl = fiscalStamp.QrCodeTextOrUrl,
            CreatedAtUtc = fiscalStamp.CreatedAtUtc,
            UpdatedAtUtc = fiscalStamp.UpdatedAtUtc
        };
    }

    private static FiscalCancellationResponse MapFiscalCancellation(FiscalCancellation fiscalCancellation)
    {
        return new FiscalCancellationResponse
        {
            FiscalDocumentId = fiscalCancellation.FiscalDocumentId,
            Status = fiscalCancellation.Status.ToString(),
            CancellationReasonCode = fiscalCancellation.CancellationReasonCode,
            ReplacementUuid = fiscalCancellation.ReplacementUuid,
            ProviderName = fiscalCancellation.ProviderName,
            ProviderCode = fiscalCancellation.ProviderCode,
            ProviderMessage = fiscalCancellation.ProviderMessage,
            ErrorCode = fiscalCancellation.ErrorCode,
            ErrorMessage = fiscalCancellation.ErrorMessage,
            RequestedAtUtc = fiscalCancellation.RequestedAtUtc,
            CancelledAtUtc = fiscalCancellation.CancelledAtUtc,
            CreatedAtUtc = fiscalCancellation.CreatedAtUtc,
            UpdatedAtUtc = fiscalCancellation.UpdatedAtUtc
        };
    }

    private static FiscalDocumentItemResponse MapItem(FiscalDocumentItem item)
    {
        return new FiscalDocumentItemResponse
        {
            Id = item.Id,
            FiscalDocumentId = item.FiscalDocumentId,
            LineNumber = item.LineNumber,
            BillingDocumentItemId = item.BillingDocumentItemId,
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
            UnitText = item.UnitText,
            CreatedAtUtc = item.CreatedAtUtc
        };
    }

    public sealed class FiscalDocumentResponse
    {
        public long Id { get; init; }
        public long BillingDocumentId { get; init; }
        public long IssuerProfileId { get; init; }
        public long FiscalReceiverId { get; init; }
        public string Status { get; init; } = string.Empty;
        public string CfdiVersion { get; init; } = string.Empty;
        public string DocumentType { get; init; } = string.Empty;
        public string? Series { get; init; }
        public string? Folio { get; init; }
        public DateTime IssuedAtUtc { get; init; }
        public string CurrencyCode { get; init; } = string.Empty;
        public decimal? ExchangeRate { get; init; }
        public string PaymentMethodSat { get; init; } = string.Empty;
        public string PaymentFormSat { get; init; } = string.Empty;
        public string? PaymentCondition { get; init; }
        public bool IsCreditSale { get; init; }
        public int? CreditDays { get; init; }
        public string IssuerRfc { get; init; } = string.Empty;
        public string IssuerLegalName { get; init; } = string.Empty;
        public string IssuerFiscalRegimeCode { get; init; } = string.Empty;
        public string IssuerPostalCode { get; init; } = string.Empty;
        public string PacEnvironment { get; init; } = string.Empty;
        public bool HasCertificateReference { get; init; }
        public bool HasPrivateKeyReference { get; init; }
        public bool HasPrivateKeyPasswordReference { get; init; }
        public string ReceiverRfc { get; init; } = string.Empty;
        public string ReceiverLegalName { get; init; } = string.Empty;
        public string ReceiverFiscalRegimeCode { get; init; } = string.Empty;
        public string ReceiverCfdiUseCode { get; init; } = string.Empty;
        public string ReceiverPostalCode { get; init; } = string.Empty;
        public string? ReceiverCountryCode { get; init; }
        public string? ReceiverForeignTaxRegistration { get; init; }
        public decimal Subtotal { get; init; }
        public decimal DiscountTotal { get; init; }
        public decimal TaxTotal { get; init; }
        public decimal Total { get; init; }
        public DateTime CreatedAtUtc { get; init; }
        public DateTime UpdatedAtUtc { get; init; }
        public IReadOnlyList<FiscalDocumentItemResponse> Items { get; init; } = [];
    }

    public sealed class StampFiscalDocumentRequest
    {
        public bool RetryRejected { get; init; }
    }

    public sealed class StampFiscalDocumentResponse
    {
        public string Outcome { get; init; } = string.Empty;
        public bool IsSuccess { get; init; }
        public string? ErrorMessage { get; init; }
        public long FiscalDocumentId { get; init; }
        public string? FiscalDocumentStatus { get; init; }
        public long? FiscalStampId { get; init; }
        public string? Uuid { get; init; }
        public DateTime? StampedAtUtc { get; init; }
        public string? ProviderName { get; init; }
        public string? ProviderTrackingId { get; init; }
    }

    public sealed class FiscalStampResponse
    {
        public long FiscalDocumentId { get; init; }
        public string ProviderName { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public string? Uuid { get; init; }
        public DateTime? StampedAtUtc { get; init; }
        public string? ProviderCode { get; init; }
        public string? ProviderMessage { get; init; }
        public string? ErrorCode { get; init; }
        public string? ErrorMessage { get; init; }
        public string? XmlHash { get; init; }
        public string? QrCodeTextOrUrl { get; init; }
        public DateTime CreatedAtUtc { get; init; }
        public DateTime UpdatedAtUtc { get; init; }
    }

    public sealed class CancelFiscalDocumentRequest
    {
        public string CancellationReasonCode { get; init; } = string.Empty;
        public string? ReplacementUuid { get; init; }
    }

    public sealed class CancelFiscalDocumentResponse
    {
        public string Outcome { get; init; } = string.Empty;
        public bool IsSuccess { get; init; }
        public string? ErrorMessage { get; init; }
        public long FiscalDocumentId { get; init; }
        public string? FiscalDocumentStatus { get; init; }
        public long? FiscalCancellationId { get; init; }
        public string? CancellationStatus { get; init; }
        public string? ProviderName { get; init; }
        public string? ProviderTrackingId { get; init; }
        public DateTime? CancelledAtUtc { get; init; }
    }

    public sealed class FiscalCancellationResponse
    {
        public long FiscalDocumentId { get; init; }
        public string Status { get; init; } = string.Empty;
        public string CancellationReasonCode { get; init; } = string.Empty;
        public string? ReplacementUuid { get; init; }
        public string ProviderName { get; init; } = string.Empty;
        public string? ProviderCode { get; init; }
        public string? ProviderMessage { get; init; }
        public string? ErrorCode { get; init; }
        public string? ErrorMessage { get; init; }
        public DateTime RequestedAtUtc { get; init; }
        public DateTime? CancelledAtUtc { get; init; }
        public DateTime CreatedAtUtc { get; init; }
        public DateTime UpdatedAtUtc { get; init; }
    }

    public sealed class RefreshFiscalDocumentStatusResponse
    {
        public string Outcome { get; init; } = string.Empty;
        public bool IsSuccess { get; init; }
        public string? ErrorMessage { get; init; }
        public long FiscalDocumentId { get; init; }
        public string? FiscalDocumentStatus { get; init; }
        public string? Uuid { get; init; }
        public string? LastKnownExternalStatus { get; init; }
        public string? ProviderCode { get; init; }
        public string? ProviderMessage { get; init; }
        public DateTime? CheckedAtUtc { get; init; }
    }

    public sealed class FiscalDocumentItemResponse
    {
        public long Id { get; init; }
        public long FiscalDocumentId { get; init; }
        public int LineNumber { get; init; }
        public long? BillingDocumentItemId { get; init; }
        public string InternalCode { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public decimal Quantity { get; init; }
        public decimal UnitPrice { get; init; }
        public decimal DiscountAmount { get; init; }
        public decimal Subtotal { get; init; }
        public decimal TaxTotal { get; init; }
        public decimal Total { get; init; }
        public string SatProductServiceCode { get; init; } = string.Empty;
        public string SatUnitCode { get; init; } = string.Empty;
        public string TaxObjectCode { get; init; } = string.Empty;
        public decimal VatRate { get; init; }
        public string? UnitText { get; init; }
        public DateTime CreatedAtUtc { get; init; }
    }
}
