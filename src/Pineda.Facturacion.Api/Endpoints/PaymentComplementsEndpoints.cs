using Microsoft.AspNetCore.Http.HttpResults;
using Pineda.Facturacion.Api.Security;
using Pineda.Facturacion.Application.Abstractions.Security;
using Pineda.Facturacion.Application.Security;
using Pineda.Facturacion.Application.UseCases.PaymentComplements;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Api.Endpoints;

public static class PaymentComplementsEndpoints
{
    public static IEndpointRouteBuilder MapPaymentComplementsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/payment-complements")
            .WithTags("PaymentComplements")
            .RequireAuthorization(AuthorizationPolicyNames.Authenticated);

        group.MapPost("/{paymentComplementId:long}/stamp", StampPaymentComplementAsync)
            .RequireAuthorization(AuthorizationPolicyNames.SupervisorOrAdmin)
            .WithName("StampPaymentComplement")
            .WithSummary("Stamp a persisted payment complement snapshot")
            .Produces<StampPaymentComplementResponse>(StatusCodes.Status200OK)
            .Produces<StampPaymentComplementResponse>(StatusCodes.Status400BadRequest)
            .Produces<StampPaymentComplementResponse>(StatusCodes.Status404NotFound)
            .Produces<StampPaymentComplementResponse>(StatusCodes.Status409Conflict)
            .Produces<StampPaymentComplementResponse>(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/{paymentComplementId:long}/stamp", GetPaymentComplementStampByPaymentComplementIdAsync)
            .WithName("GetPaymentComplementStampByPaymentComplementId")
            .WithSummary("Get persisted stamp evidence for a payment complement")
            .Produces<PaymentComplementStampResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{paymentComplementId:long}/stamp/xml", GetPaymentComplementStampXmlByPaymentComplementIdAsync)
            .WithName("GetPaymentComplementStampXmlByPaymentComplementId")
            .WithSummary("Get persisted XML evidence for a payment complement")
            .Produces(StatusCodes.Status200OK, contentType: "application/xml")
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{paymentComplementId:long}/cancel", CancelPaymentComplementAsync)
            .RequireAuthorization(AuthorizationPolicyNames.SupervisorOrAdmin)
            .WithName("CancelPaymentComplement")
            .WithSummary("Cancel a stamped payment complement")
            .Produces<CancelPaymentComplementResponse>(StatusCodes.Status200OK)
            .Produces<CancelPaymentComplementResponse>(StatusCodes.Status400BadRequest)
            .Produces<CancelPaymentComplementResponse>(StatusCodes.Status404NotFound)
            .Produces<CancelPaymentComplementResponse>(StatusCodes.Status409Conflict)
            .Produces<CancelPaymentComplementResponse>(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/{paymentComplementId:long}/cancellation", GetPaymentComplementCancellationByPaymentComplementIdAsync)
            .WithName("GetPaymentComplementCancellationByPaymentComplementId")
            .WithSummary("Get persisted cancellation evidence for a payment complement")
            .Produces<PaymentComplementCancellationResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{paymentComplementId:long}/refresh-status", RefreshPaymentComplementStatusAsync)
            .RequireAuthorization(AuthorizationPolicyNames.SupervisorOrAdmin)
            .WithName("RefreshPaymentComplementStatus")
            .WithSummary("Refresh the latest external status for a stamped payment complement")
            .Produces<RefreshPaymentComplementStatusResponse>(StatusCodes.Status200OK)
            .Produces<RefreshPaymentComplementStatusResponse>(StatusCodes.Status400BadRequest)
            .Produces<RefreshPaymentComplementStatusResponse>(StatusCodes.Status404NotFound)
            .Produces<RefreshPaymentComplementStatusResponse>(StatusCodes.Status503ServiceUnavailable);

        return endpoints;
    }

    private static async Task<Results<Ok<StampPaymentComplementResponse>, BadRequest<StampPaymentComplementResponse>, NotFound<StampPaymentComplementResponse>, Conflict<StampPaymentComplementResponse>, JsonHttpResult<StampPaymentComplementResponse>>> StampPaymentComplementAsync(
        long paymentComplementId,
        StampPaymentComplementRequest? request,
        StampPaymentComplementService service,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(
            new StampPaymentComplementCommand
            {
                PaymentComplementId = paymentComplementId,
                RetryRejected = request?.RetryRejected ?? false
            },
            cancellationToken);

        var response = new StampPaymentComplementResponse
        {
            Outcome = result.Outcome.ToString(),
            IsSuccess = result.IsSuccess,
            ErrorMessage = result.ErrorMessage,
            PaymentComplementId = result.PaymentComplementId,
            Status = result.Status?.ToString(),
            PaymentComplementStampId = result.PaymentComplementStampId,
            Uuid = result.Uuid,
            StampedAtUtc = result.StampedAtUtc,
            ProviderName = result.ProviderName,
            ProviderTrackingId = result.ProviderTrackingId
        };

        await AuditApiHelper.RecordAsync(
            auditService,
            "PaymentComplement.Stamp",
            "PaymentComplementDocument",
            paymentComplementId.ToString(),
            result.Outcome.ToString(),
            new { paymentComplementId, retryRejected = request?.RetryRejected ?? false },
            new { result.PaymentComplementStampId, result.Uuid, result.Status, result.ProviderName, result.ProviderTrackingId },
            result.ErrorMessage,
            cancellationToken);

        return result.Outcome switch
        {
            StampPaymentComplementOutcome.Stamped => TypedResults.Ok(response),
            StampPaymentComplementOutcome.NotFound => TypedResults.NotFound(response),
            StampPaymentComplementOutcome.Conflict => TypedResults.Conflict(response),
            StampPaymentComplementOutcome.ProviderRejected => TypedResults.Conflict(response),
            StampPaymentComplementOutcome.ProviderUnavailable => TypedResults.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable),
            _ => TypedResults.BadRequest(response)
        };
    }

    private static async Task<Results<Ok<PaymentComplementStampResponse>, NotFound>> GetPaymentComplementStampByPaymentComplementIdAsync(
        long paymentComplementId,
        GetPaymentComplementStampByPaymentComplementIdService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(paymentComplementId, cancellationToken);
        if (result.Outcome == GetPaymentComplementStampByPaymentComplementIdOutcome.NotFound || result.PaymentComplementStamp is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(MapPaymentComplementStamp(result.PaymentComplementStamp));
    }

    private static async Task<IResult> GetPaymentComplementStampXmlByPaymentComplementIdAsync(
        long paymentComplementId,
        GetPaymentComplementStampByPaymentComplementIdService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(paymentComplementId, cancellationToken);
        if (result.Outcome == GetPaymentComplementStampByPaymentComplementIdOutcome.NotFound
            || result.PaymentComplementStamp is null
            || string.IsNullOrWhiteSpace(result.PaymentComplementStamp.XmlContent))
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Text(result.PaymentComplementStamp.XmlContent, "application/xml");
    }

    private static async Task<Results<Ok<CancelPaymentComplementResponse>, BadRequest<CancelPaymentComplementResponse>, NotFound<CancelPaymentComplementResponse>, Conflict<CancelPaymentComplementResponse>, JsonHttpResult<CancelPaymentComplementResponse>>> CancelPaymentComplementAsync(
        long paymentComplementId,
        CancelPaymentComplementRequest request,
        CancelPaymentComplementService service,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(
            new CancelPaymentComplementCommand
            {
                PaymentComplementId = paymentComplementId,
                CancellationReasonCode = request.CancellationReasonCode,
                ReplacementUuid = request.ReplacementUuid
            },
            cancellationToken);

        var response = new CancelPaymentComplementResponse
        {
            Outcome = result.Outcome.ToString(),
            IsSuccess = result.IsSuccess,
            ErrorMessage = result.ErrorMessage,
            PaymentComplementId = result.PaymentComplementId,
            PaymentComplementStatus = result.PaymentComplementStatus?.ToString(),
            PaymentComplementCancellationId = result.PaymentComplementCancellationId,
            CancellationStatus = result.CancellationStatus?.ToString(),
            ProviderName = result.ProviderName,
            ProviderTrackingId = result.ProviderTrackingId,
            CancelledAtUtc = result.CancelledAtUtc
        };

        await AuditApiHelper.RecordAsync(
            auditService,
            "PaymentComplement.Cancel",
            "PaymentComplementDocument",
            paymentComplementId.ToString(),
            result.Outcome.ToString(),
            new { paymentComplementId, request.CancellationReasonCode, request.ReplacementUuid },
            new { result.PaymentComplementCancellationId, result.CancellationStatus, result.PaymentComplementStatus, result.ProviderName, result.ProviderTrackingId },
            result.ErrorMessage,
            cancellationToken);

        return result.Outcome switch
        {
            CancelPaymentComplementOutcome.Cancelled => TypedResults.Ok(response),
            CancelPaymentComplementOutcome.NotFound => TypedResults.NotFound(response),
            CancelPaymentComplementOutcome.Conflict => TypedResults.Conflict(response),
            CancelPaymentComplementOutcome.ProviderRejected => TypedResults.Conflict(response),
            CancelPaymentComplementOutcome.ProviderUnavailable => TypedResults.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable),
            _ => TypedResults.BadRequest(response)
        };
    }

    private static async Task<Results<Ok<PaymentComplementCancellationResponse>, NotFound>> GetPaymentComplementCancellationByPaymentComplementIdAsync(
        long paymentComplementId,
        GetPaymentComplementCancellationByPaymentComplementIdService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(paymentComplementId, cancellationToken);
        if (result.Outcome == GetPaymentComplementCancellationByPaymentComplementIdOutcome.NotFound || result.PaymentComplementCancellation is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(MapPaymentComplementCancellation(result.PaymentComplementCancellation));
    }

    private static async Task<Results<Ok<RefreshPaymentComplementStatusResponse>, BadRequest<RefreshPaymentComplementStatusResponse>, NotFound<RefreshPaymentComplementStatusResponse>, JsonHttpResult<RefreshPaymentComplementStatusResponse>>> RefreshPaymentComplementStatusAsync(
        long paymentComplementId,
        RefreshPaymentComplementStatusService service,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(
            new RefreshPaymentComplementStatusCommand
            {
                PaymentComplementId = paymentComplementId
            },
            cancellationToken);

        var response = new RefreshPaymentComplementStatusResponse
        {
            Outcome = result.Outcome.ToString(),
            IsSuccess = result.IsSuccess,
            ErrorMessage = result.ErrorMessage,
            PaymentComplementId = result.PaymentComplementId,
            PaymentComplementStatus = result.PaymentComplementStatus?.ToString(),
            Uuid = result.Uuid,
            LastKnownExternalStatus = result.LastKnownExternalStatus,
            ProviderCode = result.ProviderCode,
            ProviderMessage = result.ProviderMessage,
            CheckedAtUtc = result.CheckedAtUtc
        };

        await AuditApiHelper.RecordAsync(
            auditService,
            "PaymentComplement.RefreshStatus",
            "PaymentComplementDocument",
            paymentComplementId.ToString(),
            result.Outcome.ToString(),
            new { paymentComplementId },
            new { result.Uuid, result.LastKnownExternalStatus, result.ProviderCode, result.ProviderMessage, result.CheckedAtUtc, result.PaymentComplementStatus },
            result.ErrorMessage,
            cancellationToken);

        return result.Outcome switch
        {
            RefreshPaymentComplementStatusOutcome.Refreshed => TypedResults.Ok(response),
            RefreshPaymentComplementStatusOutcome.NotFound => TypedResults.NotFound(response),
            RefreshPaymentComplementStatusOutcome.ProviderUnavailable => TypedResults.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable),
            _ => TypedResults.BadRequest(response)
        };
    }

    public static PaymentComplementDocumentResponse MapPaymentComplement(PaymentComplementDocument document)
    {
        return new PaymentComplementDocumentResponse
        {
            Id = document.Id,
            AccountsReceivablePaymentId = document.AccountsReceivablePaymentId,
            Status = document.Status.ToString(),
            ProviderName = document.ProviderName,
            CfdiVersion = document.CfdiVersion,
            DocumentType = document.DocumentType,
            IssuedAtUtc = document.IssuedAtUtc,
            PaymentDateUtc = document.PaymentDateUtc,
            CurrencyCode = document.CurrencyCode,
            TotalPaymentsAmount = document.TotalPaymentsAmount,
            IssuerProfileId = document.IssuerProfileId,
            FiscalReceiverId = document.FiscalReceiverId,
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
            PacEnvironment = document.PacEnvironment,
            HasCertificateReference = !string.IsNullOrWhiteSpace(document.CertificateReference),
            HasPrivateKeyReference = !string.IsNullOrWhiteSpace(document.PrivateKeyReference),
            HasPrivateKeyPasswordReference = !string.IsNullOrWhiteSpace(document.PrivateKeyPasswordReference),
            CreatedAtUtc = document.CreatedAtUtc,
            UpdatedAtUtc = document.UpdatedAtUtc,
            RelatedDocuments = document.RelatedDocuments
                .OrderBy(x => x.InstallmentNumber)
                .ThenBy(x => x.Id)
                .Select(x => new PaymentComplementRelatedDocumentResponse
                {
                    Id = x.Id,
                    AccountsReceivableInvoiceId = x.AccountsReceivableInvoiceId,
                    FiscalDocumentId = x.FiscalDocumentId,
                    FiscalStampId = x.FiscalStampId,
                    RelatedDocumentUuid = x.RelatedDocumentUuid,
                    InstallmentNumber = x.InstallmentNumber,
                    PreviousBalance = x.PreviousBalance,
                    PaidAmount = x.PaidAmount,
                    RemainingBalance = x.RemainingBalance,
                    CurrencyCode = x.CurrencyCode,
                    CreatedAtUtc = x.CreatedAtUtc
                })
                .ToList()
        };
    }

    public static PaymentComplementStampResponse MapPaymentComplementStamp(PaymentComplementStamp stamp)
    {
        return new PaymentComplementStampResponse
        {
            Id = stamp.Id,
            PaymentComplementDocumentId = stamp.PaymentComplementDocumentId,
            ProviderName = stamp.ProviderName,
            ProviderOperation = stamp.ProviderOperation,
            Status = stamp.Status.ToString(),
            ProviderTrackingId = stamp.ProviderTrackingId,
            Uuid = stamp.Uuid,
            StampedAtUtc = stamp.StampedAtUtc,
            ProviderCode = stamp.ProviderCode,
            ProviderMessage = stamp.ProviderMessage,
            ErrorCode = stamp.ErrorCode,
            ErrorMessage = stamp.ErrorMessage,
            XmlHash = stamp.XmlHash,
            OriginalString = stamp.OriginalString,
            QrCodeTextOrUrl = stamp.QrCodeTextOrUrl,
            CreatedAtUtc = stamp.CreatedAtUtc,
            UpdatedAtUtc = stamp.UpdatedAtUtc
        };
    }

    public static PaymentComplementCancellationResponse MapPaymentComplementCancellation(PaymentComplementCancellation cancellation)
    {
        return new PaymentComplementCancellationResponse
        {
            PaymentComplementId = cancellation.PaymentComplementDocumentId,
            Status = cancellation.Status.ToString(),
            CancellationReasonCode = cancellation.CancellationReasonCode,
            ReplacementUuid = cancellation.ReplacementUuid,
            ProviderName = cancellation.ProviderName,
            ProviderCode = cancellation.ProviderCode,
            ProviderMessage = cancellation.ProviderMessage,
            ErrorCode = cancellation.ErrorCode,
            ErrorMessage = cancellation.ErrorMessage,
            RequestedAtUtc = cancellation.RequestedAtUtc,
            CancelledAtUtc = cancellation.CancelledAtUtc,
            CreatedAtUtc = cancellation.CreatedAtUtc,
            UpdatedAtUtc = cancellation.UpdatedAtUtc
        };
    }
}

public class StampPaymentComplementRequest
{
    public bool RetryRejected { get; set; }
}

public class StampPaymentComplementResponse
{
    public string Outcome { get; set; } = string.Empty;

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public long PaymentComplementId { get; set; }

    public string? Status { get; set; }

    public long? PaymentComplementStampId { get; set; }

    public string? Uuid { get; set; }

    public DateTime? StampedAtUtc { get; set; }

    public string? ProviderName { get; set; }

    public string? ProviderTrackingId { get; set; }
}

public class PaymentComplementDocumentResponse
{
    public long Id { get; set; }

    public long AccountsReceivablePaymentId { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? ProviderName { get; set; }

    public string CfdiVersion { get; set; } = string.Empty;

    public string DocumentType { get; set; } = string.Empty;

    public DateTime IssuedAtUtc { get; set; }

    public DateTime PaymentDateUtc { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public decimal TotalPaymentsAmount { get; set; }

    public long? IssuerProfileId { get; set; }

    public long? FiscalReceiverId { get; set; }

    public string IssuerRfc { get; set; } = string.Empty;

    public string IssuerLegalName { get; set; } = string.Empty;

    public string IssuerFiscalRegimeCode { get; set; } = string.Empty;

    public string IssuerPostalCode { get; set; } = string.Empty;

    public string ReceiverRfc { get; set; } = string.Empty;

    public string ReceiverLegalName { get; set; } = string.Empty;

    public string ReceiverFiscalRegimeCode { get; set; } = string.Empty;

    public string ReceiverPostalCode { get; set; } = string.Empty;

    public string? ReceiverCountryCode { get; set; }

    public string? ReceiverForeignTaxRegistration { get; set; }

    public string PacEnvironment { get; set; } = string.Empty;

    public bool HasCertificateReference { get; set; }

    public bool HasPrivateKeyReference { get; set; }

    public bool HasPrivateKeyPasswordReference { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public List<PaymentComplementRelatedDocumentResponse> RelatedDocuments { get; set; } = [];
}

public class PaymentComplementRelatedDocumentResponse
{
    public long Id { get; set; }

    public long AccountsReceivableInvoiceId { get; set; }

    public long FiscalDocumentId { get; set; }

    public long FiscalStampId { get; set; }

    public string RelatedDocumentUuid { get; set; } = string.Empty;

    public int InstallmentNumber { get; set; }

    public decimal PreviousBalance { get; set; }

    public decimal PaidAmount { get; set; }

    public decimal RemainingBalance { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}

public class PaymentComplementStampResponse
{
    public long Id { get; set; }

    public long PaymentComplementDocumentId { get; set; }

    public string ProviderName { get; set; } = string.Empty;

    public string ProviderOperation { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? ProviderTrackingId { get; set; }

    public string? Uuid { get; set; }

    public DateTime? StampedAtUtc { get; set; }

    public string? ProviderCode { get; set; }

    public string? ProviderMessage { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }

    public string? XmlHash { get; set; }

    public string? OriginalString { get; set; }

    public string? QrCodeTextOrUrl { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}

public class CancelPaymentComplementRequest
{
    public string CancellationReasonCode { get; set; } = string.Empty;

    public string? ReplacementUuid { get; set; }
}

public class CancelPaymentComplementResponse
{
    public string Outcome { get; set; } = string.Empty;

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public long PaymentComplementId { get; set; }

    public string? PaymentComplementStatus { get; set; }

    public long? PaymentComplementCancellationId { get; set; }

    public string? CancellationStatus { get; set; }

    public string? ProviderName { get; set; }

    public string? ProviderTrackingId { get; set; }

    public DateTime? CancelledAtUtc { get; set; }
}

public class PaymentComplementCancellationResponse
{
    public long PaymentComplementId { get; set; }

    public string Status { get; set; } = string.Empty;

    public string CancellationReasonCode { get; set; } = string.Empty;

    public string? ReplacementUuid { get; set; }

    public string ProviderName { get; set; } = string.Empty;

    public string? ProviderCode { get; set; }

    public string? ProviderMessage { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime RequestedAtUtc { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}

public class RefreshPaymentComplementStatusResponse
{
    public string Outcome { get; set; } = string.Empty;

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public long PaymentComplementId { get; set; }

    public string? PaymentComplementStatus { get; set; }

    public string? Uuid { get; set; }

    public string? LastKnownExternalStatus { get; set; }

    public string? ProviderCode { get; set; }

    public string? ProviderMessage { get; set; }

    public DateTime? CheckedAtUtc { get; set; }
}
