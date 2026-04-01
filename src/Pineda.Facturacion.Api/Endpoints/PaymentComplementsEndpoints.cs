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

        group.MapGet("/base-documents/internal", SearchInternalRepBaseDocumentsAsync)
            .WithName("SearchInternalRepBaseDocuments")
            .WithSummary("Search internal CFDI base documents for operational REP follow-up")
            .Produces<InternalRepBaseDocumentListResponse>(StatusCodes.Status200OK)
            .Produces<PaymentComplementBaseDocumentSearchErrorResponse>(StatusCodes.Status400BadRequest);

        group.MapGet("/base-documents/internal/{fiscalDocumentId:long}", GetInternalRepBaseDocumentByFiscalDocumentIdAsync)
            .WithName("GetInternalRepBaseDocumentByFiscalDocumentId")
            .WithSummary("Get internal CFDI base document REP context by fiscal document id")
            .Produces<InternalRepBaseDocumentDetailResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<Results<Ok<InternalRepBaseDocumentListResponse>, BadRequest<PaymentComplementBaseDocumentSearchErrorResponse>>> SearchInternalRepBaseDocumentsAsync(
        int? page,
        int? pageSize,
        DateOnly? fromDate,
        DateOnly? toDate,
        string? receiverRfc,
        string? query,
        bool? eligible,
        bool? blocked,
        bool? withOutstandingBalance,
        bool? hasRepEmitted,
        SearchInternalRepBaseDocumentsService service,
        CancellationToken cancellationToken)
    {
        if (fromDate.HasValue && toDate.HasValue && fromDate.Value > toDate.Value)
        {
            return TypedResults.BadRequest(new PaymentComplementBaseDocumentSearchErrorResponse
            {
                ErrorMessage = "La fecha inicial no puede ser mayor a la fecha final."
            });
        }

        var result = await service.ExecuteAsync(
            new SearchInternalRepBaseDocumentsFilter
            {
                Page = page ?? 1,
                PageSize = pageSize ?? 25,
                FromDate = fromDate,
                ToDate = toDate,
                ReceiverRfc = receiverRfc,
                Query = query,
                Eligible = eligible,
                Blocked = blocked,
                WithOutstandingBalance = withOutstandingBalance,
                HasRepEmitted = hasRepEmitted
            },
            cancellationToken);

        return TypedResults.Ok(new InternalRepBaseDocumentListResponse
        {
            Page = result.Page,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount,
            TotalPages = result.TotalPages,
            Items = result.Items.Select(MapInternalRepBaseDocument).ToList()
        });
    }

    private static async Task<Results<Ok<InternalRepBaseDocumentDetailResponse>, NotFound>> GetInternalRepBaseDocumentByFiscalDocumentIdAsync(
        long fiscalDocumentId,
        GetInternalRepBaseDocumentByFiscalDocumentIdService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(fiscalDocumentId, cancellationToken);
        if (result.Outcome == GetInternalRepBaseDocumentByFiscalDocumentIdOutcome.NotFound || result.Document is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(new InternalRepBaseDocumentDetailResponse
        {
            Summary = MapInternalRepBaseDocument(result.Document.Summary),
            PaymentApplications = result.Document.PaymentApplications
                .Select(x => new InternalRepBaseDocumentPaymentApplicationResponse
                {
                    AccountsReceivablePaymentId = x.AccountsReceivablePaymentId,
                    ApplicationSequence = x.ApplicationSequence,
                    PaymentDateUtc = x.PaymentDateUtc,
                    PaymentFormSat = x.PaymentFormSat,
                    AppliedAmount = x.AppliedAmount,
                    PreviousBalance = x.PreviousBalance,
                    NewBalance = x.NewBalance,
                    Reference = x.Reference,
                    CreatedAtUtc = x.CreatedAtUtc
                })
                .ToList(),
            PaymentComplements = result.Document.PaymentComplements
                .Select(x => new InternalRepBaseDocumentPaymentComplementResponse
                {
                    PaymentComplementId = x.PaymentComplementId,
                    AccountsReceivablePaymentId = x.AccountsReceivablePaymentId,
                    Status = x.Status,
                    Uuid = x.Uuid,
                    PaymentDateUtc = x.PaymentDateUtc,
                    IssuedAtUtc = x.IssuedAtUtc,
                    StampedAtUtc = x.StampedAtUtc,
                    PaidAmount = x.PaidAmount
                })
                .ToList()
        });
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
            ProviderTrackingId = result.ProviderTrackingId,
            ProviderCode = result.ProviderCode,
            ProviderMessage = result.ProviderMessage,
            ErrorCode = result.ErrorCode,
            SupportMessage = result.SupportMessage,
            RawResponseSummaryJson = result.RawResponseSummaryJson
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
            CancelledAtUtc = result.CancelledAtUtc,
            ProviderCode = result.ProviderCode,
            ProviderMessage = result.ProviderMessage,
            ErrorCode = result.ErrorCode,
            SupportMessage = result.SupportMessage,
            RawResponseSummaryJson = result.RawResponseSummaryJson
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
            CheckedAtUtc = result.CheckedAtUtc,
            SupportMessage = result.SupportMessage,
            RawResponseSummaryJson = result.RawResponseSummaryJson
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
            AppliesToIncomePpdInvoices = true,
            EligibilitySummary = BuildComplementEligibilitySummary(document),
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
            SupportMessage = BuildPaymentComplementStampSupportMessage(stamp),
            RawResponseSummaryJson = stamp.RawResponseSummaryJson,
            XmlHash = stamp.XmlHash,
            OriginalString = stamp.OriginalString,
            QrCodeTextOrUrl = stamp.QrCodeTextOrUrl,
            LastKnownExternalStatus = stamp.LastKnownExternalStatus,
            LastStatusProviderCode = stamp.LastStatusProviderCode,
            LastStatusProviderMessage = stamp.LastStatusProviderMessage,
            LastStatusSupportMessage = BuildPaymentComplementStatusSupportMessage(stamp),
            LastStatusRawResponseSummaryJson = stamp.LastStatusRawResponseSummaryJson,
            LastStatusCheckAtUtc = stamp.LastStatusCheckAtUtc,
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
            ProviderTrackingId = cancellation.ProviderTrackingId,
            ProviderCode = cancellation.ProviderCode,
            ProviderMessage = cancellation.ProviderMessage,
            ErrorCode = cancellation.ErrorCode,
            ErrorMessage = cancellation.ErrorMessage,
            SupportMessage = BuildPaymentComplementCancellationSupportMessage(cancellation),
            RawResponseSummaryJson = cancellation.RawResponseSummaryJson,
            RequestedAtUtc = cancellation.RequestedAtUtc,
            CancelledAtUtc = cancellation.CancelledAtUtc,
            CreatedAtUtc = cancellation.CreatedAtUtc,
            UpdatedAtUtc = cancellation.UpdatedAtUtc
        };
    }

    public static InternalRepBaseDocumentItemResponse MapInternalRepBaseDocument(InternalRepBaseDocumentListItem item)
    {
        return new InternalRepBaseDocumentItemResponse
        {
            FiscalDocumentId = item.FiscalDocumentId,
            BillingDocumentId = item.BillingDocumentId,
            SalesOrderId = item.SalesOrderId,
            AccountsReceivableInvoiceId = item.AccountsReceivableInvoiceId,
            FiscalStampId = item.FiscalStampId,
            Uuid = item.Uuid,
            Series = item.Series,
            Folio = item.Folio,
            ReceiverRfc = item.ReceiverRfc,
            ReceiverLegalName = item.ReceiverLegalName,
            IssuedAtUtc = item.IssuedAtUtc,
            PaymentMethodSat = item.PaymentMethodSat,
            PaymentFormSat = item.PaymentFormSat,
            CurrencyCode = item.CurrencyCode,
            Total = item.Total,
            PaidTotal = item.PaidTotal,
            OutstandingBalance = item.OutstandingBalance,
            FiscalStatus = item.FiscalStatus,
            AccountsReceivableStatus = item.AccountsReceivableStatus,
            RepOperationalStatus = item.RepOperationalStatus,
            IsEligible = item.IsEligible,
            IsBlocked = item.IsBlocked,
            EligibilityReason = item.EligibilityReason,
            RegisteredPaymentCount = item.RegisteredPaymentCount,
            PaymentComplementCount = item.PaymentComplementCount,
            StampedPaymentComplementCount = item.StampedPaymentComplementCount
        };
    }

    private static string BuildComplementEligibilitySummary(PaymentComplementDocument document)
    {
        return $"Formalizado para CFDI de ingreso con MetodoPago PPD y FormaPago 99. CFDI base relacionados: {document.RelatedDocuments.Count}.";
    }

    private static string BuildPaymentComplementStampSupportMessage(PaymentComplementStamp stamp)
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

        return parts.Count == 0 ? "Sin metadatos adicionales de timbrado." : string.Join(" | ", parts);
    }

    private static string? BuildPaymentComplementStatusSupportMessage(PaymentComplementStamp stamp)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(stamp.LastStatusProviderCode))
        {
            parts.Add($"Código proveedor: {stamp.LastStatusProviderCode}");
        }

        if (!string.IsNullOrWhiteSpace(stamp.LastStatusProviderMessage))
        {
            parts.Add($"Mensaje proveedor: {stamp.LastStatusProviderMessage}");
        }

        if (!string.IsNullOrWhiteSpace(stamp.LastKnownExternalStatus))
        {
            parts.Add($"Estado externo: {stamp.LastKnownExternalStatus}");
        }

        return parts.Count == 0 ? null : string.Join(" | ", parts);
    }

    private static string BuildPaymentComplementCancellationSupportMessage(PaymentComplementCancellation cancellation)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(cancellation.ProviderCode))
        {
            parts.Add($"Código proveedor: {cancellation.ProviderCode}");
        }

        if (!string.IsNullOrWhiteSpace(cancellation.ProviderMessage))
        {
            parts.Add($"Mensaje proveedor: {cancellation.ProviderMessage}");
        }

        if (!string.IsNullOrWhiteSpace(cancellation.ErrorCode))
        {
            parts.Add($"Error: {cancellation.ErrorCode}");
        }

        if (!string.IsNullOrWhiteSpace(cancellation.ProviderTrackingId))
        {
            parts.Add($"Tracking: {cancellation.ProviderTrackingId}");
        }

        parts.Add($"Motivo SAT: {cancellation.CancellationReasonCode}");
        return string.Join(" | ", parts);
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

    public string? ProviderCode { get; set; }

    public string? ProviderMessage { get; set; }

    public string? ErrorCode { get; set; }

    public string? SupportMessage { get; set; }

    public string? RawResponseSummaryJson { get; set; }
}

public class PaymentComplementDocumentResponse
{
    public long Id { get; set; }

    public long AccountsReceivablePaymentId { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? ProviderName { get; set; }

    public string CfdiVersion { get; set; } = string.Empty;

    public string DocumentType { get; set; } = string.Empty;

    public bool AppliesToIncomePpdInvoices { get; set; }

    public string EligibilitySummary { get; set; } = string.Empty;

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

    public string? SupportMessage { get; set; }

    public string? RawResponseSummaryJson { get; set; }

    public string? XmlHash { get; set; }

    public string? OriginalString { get; set; }

    public string? QrCodeTextOrUrl { get; set; }

    public string? LastKnownExternalStatus { get; set; }

    public string? LastStatusProviderCode { get; set; }

    public string? LastStatusProviderMessage { get; set; }

    public string? LastStatusSupportMessage { get; set; }

    public string? LastStatusRawResponseSummaryJson { get; set; }

    public DateTime? LastStatusCheckAtUtc { get; set; }

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

    public string? ProviderCode { get; set; }

    public string? ProviderMessage { get; set; }

    public string? ErrorCode { get; set; }

    public string? SupportMessage { get; set; }

    public string? RawResponseSummaryJson { get; set; }

    public DateTime? CancelledAtUtc { get; set; }
}

public class PaymentComplementCancellationResponse
{
    public long PaymentComplementId { get; set; }

    public string Status { get; set; } = string.Empty;

    public string CancellationReasonCode { get; set; } = string.Empty;

    public string? ReplacementUuid { get; set; }

    public string ProviderName { get; set; } = string.Empty;

    public string? ProviderTrackingId { get; set; }

    public string? ProviderCode { get; set; }

    public string? ProviderMessage { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }

    public string? SupportMessage { get; set; }

    public string? RawResponseSummaryJson { get; set; }

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

    public string? SupportMessage { get; set; }

    public string? RawResponseSummaryJson { get; set; }
}

public sealed class PaymentComplementBaseDocumentSearchErrorResponse
{
    public string ErrorMessage { get; set; } = string.Empty;
}

public sealed class InternalRepBaseDocumentListResponse
{
    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }

    public List<InternalRepBaseDocumentItemResponse> Items { get; set; } = [];
}

public sealed class InternalRepBaseDocumentItemResponse
{
    public long FiscalDocumentId { get; set; }

    public long? BillingDocumentId { get; set; }

    public long? SalesOrderId { get; set; }

    public long? AccountsReceivableInvoiceId { get; set; }

    public long? FiscalStampId { get; set; }

    public string? Uuid { get; set; }

    public string Series { get; set; } = string.Empty;

    public string Folio { get; set; } = string.Empty;

    public string ReceiverRfc { get; set; } = string.Empty;

    public string ReceiverLegalName { get; set; } = string.Empty;

    public DateTime IssuedAtUtc { get; set; }

    public string PaymentMethodSat { get; set; } = string.Empty;

    public string PaymentFormSat { get; set; } = string.Empty;

    public string CurrencyCode { get; set; } = string.Empty;

    public decimal Total { get; set; }

    public decimal PaidTotal { get; set; }

    public decimal OutstandingBalance { get; set; }

    public string FiscalStatus { get; set; } = string.Empty;

    public string? AccountsReceivableStatus { get; set; }

    public string RepOperationalStatus { get; set; } = string.Empty;

    public bool IsEligible { get; set; }

    public bool IsBlocked { get; set; }

    public string EligibilityReason { get; set; } = string.Empty;

    public int RegisteredPaymentCount { get; set; }

    public int PaymentComplementCount { get; set; }

    public int StampedPaymentComplementCount { get; set; }
}

public sealed class InternalRepBaseDocumentDetailResponse
{
    public InternalRepBaseDocumentItemResponse Summary { get; set; } = new();

    public List<InternalRepBaseDocumentPaymentApplicationResponse> PaymentApplications { get; set; } = [];

    public List<InternalRepBaseDocumentPaymentComplementResponse> PaymentComplements { get; set; } = [];
}

public sealed class InternalRepBaseDocumentPaymentApplicationResponse
{
    public long AccountsReceivablePaymentId { get; set; }

    public int ApplicationSequence { get; set; }

    public DateTime PaymentDateUtc { get; set; }

    public string PaymentFormSat { get; set; } = string.Empty;

    public decimal AppliedAmount { get; set; }

    public decimal PreviousBalance { get; set; }

    public decimal NewBalance { get; set; }

    public string? Reference { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}

public sealed class InternalRepBaseDocumentPaymentComplementResponse
{
    public long PaymentComplementId { get; set; }

    public long AccountsReceivablePaymentId { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? Uuid { get; set; }

    public DateTime PaymentDateUtc { get; set; }

    public DateTime? IssuedAtUtc { get; set; }

    public DateTime? StampedAtUtc { get; set; }

    public decimal PaidAmount { get; set; }
}
