using Microsoft.AspNetCore.Http.HttpResults;
using Pineda.Facturacion.Api.Security;
using Pineda.Facturacion.Application.Abstractions.Security;
using Pineda.Facturacion.Application.Security;
using Pineda.Facturacion.Application.UseCases.PaymentComplements;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

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

        group.MapGet("/base-documents/external", SearchExternalRepBaseDocumentsAsync)
            .WithName("SearchExternalRepBaseDocuments")
            .WithSummary("Search imported external CFDI base documents for REP follow-up")
            .Produces<ExternalRepBaseDocumentListResponse>(StatusCodes.Status200OK)
            .Produces<PaymentComplementBaseDocumentSearchErrorResponse>(StatusCodes.Status400BadRequest);

        group.MapGet("/base-documents", SearchRepBaseDocumentsAsync)
            .WithName("SearchRepBaseDocuments")
            .WithSummary("Search unified internal and external REP base documents")
            .Produces<RepBaseDocumentListResponse>(StatusCodes.Status200OK)
            .Produces<PaymentComplementBaseDocumentSearchErrorResponse>(StatusCodes.Status400BadRequest);

        group.MapPost("/base-documents/internal/{fiscalDocumentId:long}/payments", RegisterInternalRepBaseDocumentPaymentAsync)
            .RequireAuthorization(AuthorizationPolicyNames.OperatorOrAbove)
            .WithName("RegisterInternalRepBaseDocumentPayment")
            .WithSummary("Register and apply a payment from the internal REP base-document context")
            .Produces<RegisterInternalRepBaseDocumentPaymentResponse>(StatusCodes.Status200OK)
            .Produces<RegisterInternalRepBaseDocumentPaymentResponse>(StatusCodes.Status400BadRequest)
            .Produces<RegisterInternalRepBaseDocumentPaymentResponse>(StatusCodes.Status404NotFound)
            .Produces<RegisterInternalRepBaseDocumentPaymentResponse>(StatusCodes.Status409Conflict);

        group.MapPost("/base-documents/internal/{fiscalDocumentId:long}/prepare", PrepareInternalRepBaseDocumentPaymentComplementAsync)
            .RequireAuthorization(AuthorizationPolicyNames.OperatorOrAbove)
            .WithName("PrepareInternalRepBaseDocumentPaymentComplement")
            .WithSummary("Prepare a payment complement from the internal REP base-document context")
            .Produces<PrepareInternalRepBaseDocumentPaymentComplementResponse>(StatusCodes.Status200OK)
            .Produces<PrepareInternalRepBaseDocumentPaymentComplementResponse>(StatusCodes.Status400BadRequest)
            .Produces<PrepareInternalRepBaseDocumentPaymentComplementResponse>(StatusCodes.Status404NotFound)
            .Produces<PrepareInternalRepBaseDocumentPaymentComplementResponse>(StatusCodes.Status409Conflict);

        group.MapPost("/base-documents/internal/{fiscalDocumentId:long}/stamp", StampInternalRepBaseDocumentPaymentComplementAsync)
            .RequireAuthorization(AuthorizationPolicyNames.SupervisorOrAdmin)
            .WithName("StampInternalRepBaseDocumentPaymentComplement")
            .WithSummary("Stamp a prepared payment complement from the internal REP base-document context")
            .Produces<StampInternalRepBaseDocumentPaymentComplementResponse>(StatusCodes.Status200OK)
            .Produces<StampInternalRepBaseDocumentPaymentComplementResponse>(StatusCodes.Status400BadRequest)
            .Produces<StampInternalRepBaseDocumentPaymentComplementResponse>(StatusCodes.Status404NotFound)
            .Produces<StampInternalRepBaseDocumentPaymentComplementResponse>(StatusCodes.Status409Conflict)
            .Produces<StampInternalRepBaseDocumentPaymentComplementResponse>(StatusCodes.Status503ServiceUnavailable);

        group.MapPost("/external-base-documents/import-xml", ImportExternalRepBaseDocumentXmlAsync)
            .RequireAuthorization(AuthorizationPolicyNames.OperatorOrAbove)
            .DisableAntiforgery()
            .WithName("ImportExternalRepBaseDocumentXml")
            .WithSummary("Import and validate an external CFDI XML for future REP administration")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<ExternalRepBaseDocumentImportResponse>(StatusCodes.Status200OK)
            .Produces<ExternalRepBaseDocumentImportResponse>(StatusCodes.Status400BadRequest)
            .Produces<ExternalRepBaseDocumentImportResponse>(StatusCodes.Status409Conflict);

        group.MapGet("/external-base-documents/{externalRepBaseDocumentId:long}", GetExternalRepBaseDocumentByIdAsync)
            .WithName("GetExternalRepBaseDocumentById")
            .WithSummary("Get imported external CFDI base document detail")
            .Produces<ExternalRepBaseDocumentDetailResponse>(StatusCodes.Status200OK)
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
            OperationalState = result.Document.OperationalState is null ? null : MapInternalRepOperationalState(result.Document.OperationalState),
            PaymentHistory = result.Document.PaymentHistory
                .Select(x => new InternalRepBaseDocumentPaymentHistoryResponse
                {
                    AccountsReceivablePaymentId = x.AccountsReceivablePaymentId,
                    PaymentDateUtc = x.PaymentDateUtc,
                    PaymentFormSat = x.PaymentFormSat,
                    PaymentAmount = x.PaymentAmount,
                    AmountAppliedToDocument = x.AmountAppliedToDocument,
                    RemainingPaymentAmount = x.RemainingPaymentAmount,
                    Reference = x.Reference,
                    Notes = x.Notes,
                    PaymentComplementId = x.PaymentComplementId,
                    PaymentComplementStatus = x.PaymentComplementStatus,
                    PaymentComplementUuid = x.PaymentComplementUuid,
                    CreatedAtUtc = x.CreatedAtUtc
                })
                .ToList(),
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
                    Notes = x.Notes,
                    PaymentAmount = x.PaymentAmount,
                    RemainingPaymentAmount = x.RemainingPaymentAmount,
                    CreatedAtUtc = x.CreatedAtUtc
                })
                .ToList(),
            IssuedReps = result.Document.PaymentComplements
                .Select(x => new InternalRepBaseDocumentPaymentComplementResponse
                {
                    PaymentComplementId = x.PaymentComplementId,
                    AccountsReceivablePaymentId = x.AccountsReceivablePaymentId,
                    Status = x.Status,
                    Uuid = x.Uuid,
                    PaymentDateUtc = x.PaymentDateUtc,
                    IssuedAtUtc = x.IssuedAtUtc,
                    StampedAtUtc = x.StampedAtUtc,
                    CancelledAtUtc = x.CancelledAtUtc,
                    ProviderName = x.ProviderName,
                    InstallmentNumber = x.InstallmentNumber,
                    PreviousBalance = x.PreviousBalance,
                    PaidAmount = x.PaidAmount,
                    RemainingBalance = x.RemainingBalance
                })
                .ToList()
        });
    }

    private static async Task<Results<Ok<ExternalRepBaseDocumentListResponse>, BadRequest<PaymentComplementBaseDocumentSearchErrorResponse>>> SearchExternalRepBaseDocumentsAsync(
        int? page,
        int? pageSize,
        DateOnly? fromDate,
        DateOnly? toDate,
        string? receiverRfc,
        string? query,
        string? validationStatus,
        bool? eligible,
        bool? blocked,
        SearchExternalRepBaseDocumentsService service,
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
            new SearchExternalRepBaseDocumentsFilter
            {
                Page = page ?? 1,
                PageSize = pageSize ?? 25,
                FromDate = fromDate,
                ToDate = toDate,
                ReceiverRfc = receiverRfc,
                Query = query,
                ValidationStatus = validationStatus,
                Eligible = eligible,
                Blocked = blocked
            },
            cancellationToken);

        return TypedResults.Ok(new ExternalRepBaseDocumentListResponse
        {
            Page = result.Page,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount,
            TotalPages = result.TotalPages,
            Items = result.Items.Select(MapExternalRepBaseDocumentListItem).ToList()
        });
    }

    private static async Task<Results<Ok<RepBaseDocumentListResponse>, BadRequest<PaymentComplementBaseDocumentSearchErrorResponse>>> SearchRepBaseDocumentsAsync(
        int? page,
        int? pageSize,
        DateOnly? fromDate,
        DateOnly? toDate,
        string? receiverRfc,
        string? query,
        string? sourceType,
        string? validationStatus,
        bool? eligible,
        bool? blocked,
        SearchRepBaseDocumentsService service,
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
            new SearchRepBaseDocumentsFilter
            {
                Page = page ?? 1,
                PageSize = pageSize ?? 25,
                FromDate = fromDate,
                ToDate = toDate,
                ReceiverRfc = receiverRfc,
                Query = query,
                SourceType = sourceType,
                ValidationStatus = validationStatus,
                Eligible = eligible,
                Blocked = blocked
            },
            cancellationToken);

        return TypedResults.Ok(new RepBaseDocumentListResponse
        {
            Page = result.Page,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount,
            TotalPages = result.TotalPages,
            Items = result.Items.Select(MapRepBaseDocument).ToList()
        });
    }

    private static async Task<Results<Ok<RegisterInternalRepBaseDocumentPaymentResponse>, BadRequest<RegisterInternalRepBaseDocumentPaymentResponse>, NotFound<RegisterInternalRepBaseDocumentPaymentResponse>, Conflict<RegisterInternalRepBaseDocumentPaymentResponse>>> RegisterInternalRepBaseDocumentPaymentAsync(
        long fiscalDocumentId,
        RegisterInternalRepBaseDocumentPaymentRequest request,
        RegisterInternalRepBaseDocumentPaymentService service,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var paymentDateUtc = DateTime.SpecifyKind(request.PaymentDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var result = await service.ExecuteAsync(
            new RegisterInternalRepBaseDocumentPaymentCommand
            {
                FiscalDocumentId = fiscalDocumentId,
                PaymentDateUtc = paymentDateUtc,
                PaymentFormSat = request.PaymentFormSat,
                Amount = request.Amount,
                Reference = request.Reference,
                Notes = request.Notes
            },
            cancellationToken);

        var response = new RegisterInternalRepBaseDocumentPaymentResponse
        {
            Outcome = result.Outcome.ToString(),
            IsSuccess = result.IsSuccess,
            ErrorMessage = result.ErrorMessage,
            WarningMessages = result.WarningMessages,
            FiscalDocumentId = result.FiscalDocumentId,
            AccountsReceivableInvoiceId = result.AccountsReceivableInvoiceId,
            AccountsReceivablePaymentId = result.AccountsReceivablePaymentId,
            AppliedAmount = result.AppliedAmount,
            RemainingBalance = result.RemainingBalance,
            RemainingPaymentAmount = result.RemainingPaymentAmount,
            RepOperationalStatus = result.UpdatedSummary?.RepOperationalStatus,
            IsEligible = result.UpdatedSummary?.IsEligible,
            IsBlocked = result.UpdatedSummary?.IsBlocked,
            EligibilityReason = result.UpdatedSummary?.EligibilityReason,
            OperationalState = result.OperationalState is null ? null : MapInternalRepOperationalState(result.OperationalState),
            Applications = result.Applications
                .OrderBy(x => x.ApplicationSequence)
                .Select(x => new RegisterInternalRepBaseDocumentPaymentApplicationResponse
                {
                    ApplicationId = x.Id,
                    AccountsReceivablePaymentId = x.AccountsReceivablePaymentId,
                    AccountsReceivableInvoiceId = x.AccountsReceivableInvoiceId,
                    ApplicationSequence = x.ApplicationSequence,
                    AppliedAmount = x.AppliedAmount,
                    PreviousBalance = x.PreviousBalance,
                    NewBalance = x.NewBalance
                })
                .ToList()
        };

        await AuditApiHelper.RecordAsync(
            auditService,
            "InternalRepBaseDocumentPayment.RegisterAndApply",
            "FiscalDocument",
            fiscalDocumentId.ToString(),
            result.Outcome.ToString(),
            new { fiscalDocumentId, request.PaymentDate, request.PaymentFormSat, request.Amount, request.Reference },
            new { result.AccountsReceivableInvoiceId, result.AccountsReceivablePaymentId, result.AppliedAmount, result.RemainingBalance, result.RemainingPaymentAmount, result.UpdatedSummary?.RepOperationalStatus },
            result.ErrorMessage,
            cancellationToken);

        return result.Outcome switch
        {
            RegisterInternalRepBaseDocumentPaymentOutcome.RegisteredAndApplied => TypedResults.Ok(response),
            RegisterInternalRepBaseDocumentPaymentOutcome.NotFound => TypedResults.NotFound(response),
            RegisterInternalRepBaseDocumentPaymentOutcome.Conflict => TypedResults.Conflict(response),
            _ => TypedResults.BadRequest(response)
        };
    }

    private static async Task<Results<Ok<PrepareInternalRepBaseDocumentPaymentComplementResponse>, BadRequest<PrepareInternalRepBaseDocumentPaymentComplementResponse>, NotFound<PrepareInternalRepBaseDocumentPaymentComplementResponse>, Conflict<PrepareInternalRepBaseDocumentPaymentComplementResponse>>> PrepareInternalRepBaseDocumentPaymentComplementAsync(
        long fiscalDocumentId,
        PrepareInternalRepBaseDocumentPaymentComplementRequest? request,
        PrepareInternalRepBaseDocumentPaymentComplementService service,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(
            new PrepareInternalRepBaseDocumentPaymentComplementCommand
            {
                FiscalDocumentId = fiscalDocumentId,
                AccountsReceivablePaymentId = request?.AccountsReceivablePaymentId
            },
            cancellationToken);

        var response = new PrepareInternalRepBaseDocumentPaymentComplementResponse
        {
            Outcome = result.Outcome.ToString(),
            IsSuccess = result.IsSuccess,
            ErrorMessage = result.ErrorMessage,
            WarningMessages = result.WarningMessages,
            FiscalDocumentId = result.FiscalDocumentId,
            AccountsReceivablePaymentId = result.AccountsReceivablePaymentId,
            PaymentComplementDocumentId = result.PaymentComplementDocumentId,
            Status = result.Status,
            RelatedDocumentCount = result.RelatedDocumentCount,
            OperationalState = result.OperationalState is null ? null : MapInternalRepOperationalState(result.OperationalState)
        };

        await AuditApiHelper.RecordAsync(
            auditService,
            "InternalRepBaseDocumentPaymentComplement.Prepare",
            "FiscalDocument",
            fiscalDocumentId.ToString(),
            result.Outcome.ToString(),
            new { fiscalDocumentId, request?.AccountsReceivablePaymentId },
            new { result.AccountsReceivablePaymentId, result.PaymentComplementDocumentId, result.Status, result.RelatedDocumentCount },
            result.ErrorMessage,
            cancellationToken);

        return result.Outcome switch
        {
            PrepareInternalRepBaseDocumentPaymentComplementOutcome.Prepared => TypedResults.Ok(response),
            PrepareInternalRepBaseDocumentPaymentComplementOutcome.AlreadyPrepared => TypedResults.Ok(response),
            PrepareInternalRepBaseDocumentPaymentComplementOutcome.NotFound => TypedResults.NotFound(response),
            PrepareInternalRepBaseDocumentPaymentComplementOutcome.Conflict => TypedResults.Conflict(response),
            _ => TypedResults.BadRequest(response)
        };
    }

    private static async Task<Results<Ok<StampInternalRepBaseDocumentPaymentComplementResponse>, BadRequest<StampInternalRepBaseDocumentPaymentComplementResponse>, NotFound<StampInternalRepBaseDocumentPaymentComplementResponse>, Conflict<StampInternalRepBaseDocumentPaymentComplementResponse>, JsonHttpResult<StampInternalRepBaseDocumentPaymentComplementResponse>>> StampInternalRepBaseDocumentPaymentComplementAsync(
        long fiscalDocumentId,
        StampInternalRepBaseDocumentPaymentComplementRequest? request,
        StampInternalRepBaseDocumentPaymentComplementService service,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(
            new StampInternalRepBaseDocumentPaymentComplementCommand
            {
                FiscalDocumentId = fiscalDocumentId,
                PaymentComplementDocumentId = request?.PaymentComplementDocumentId,
                RetryRejected = request?.RetryRejected ?? false
            },
            cancellationToken);

        var response = new StampInternalRepBaseDocumentPaymentComplementResponse
        {
            Outcome = result.Outcome.ToString(),
            IsSuccess = result.IsSuccess,
            ErrorMessage = result.ErrorMessage,
            WarningMessages = result.WarningMessages,
            FiscalDocumentId = result.FiscalDocumentId,
            AccountsReceivablePaymentId = result.AccountsReceivablePaymentId,
            PaymentComplementDocumentId = result.PaymentComplementDocumentId,
            Status = result.Status,
            PaymentComplementStampId = result.PaymentComplementStampId,
            StampUuid = result.StampUuid,
            StampedAtUtc = result.StampedAtUtc,
            XmlAvailable = result.XmlAvailable,
            OperationalState = result.OperationalState is null ? null : MapInternalRepOperationalState(result.OperationalState)
        };

        await AuditApiHelper.RecordAsync(
            auditService,
            "InternalRepBaseDocumentPaymentComplement.Stamp",
            "FiscalDocument",
            fiscalDocumentId.ToString(),
            result.Outcome.ToString(),
            new { fiscalDocumentId, request?.PaymentComplementDocumentId, request?.RetryRejected },
            new { result.AccountsReceivablePaymentId, result.PaymentComplementDocumentId, result.Status, result.StampUuid, result.StampedAtUtc },
            result.ErrorMessage,
            cancellationToken);

        return result.Outcome switch
        {
            StampInternalRepBaseDocumentPaymentComplementOutcome.Stamped => TypedResults.Ok(response),
            StampInternalRepBaseDocumentPaymentComplementOutcome.AlreadyStamped => TypedResults.Ok(response),
            StampInternalRepBaseDocumentPaymentComplementOutcome.NotFound => TypedResults.NotFound(response),
            StampInternalRepBaseDocumentPaymentComplementOutcome.Conflict => TypedResults.Conflict(response),
            StampInternalRepBaseDocumentPaymentComplementOutcome.ProviderRejected => TypedResults.Conflict(response),
            StampInternalRepBaseDocumentPaymentComplementOutcome.ProviderUnavailable => TypedResults.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable),
            _ => TypedResults.BadRequest(response)
        };
    }

    private static async Task<Results<Ok<ExternalRepBaseDocumentImportResponse>, BadRequest<ExternalRepBaseDocumentImportResponse>, Conflict<ExternalRepBaseDocumentImportResponse>>> ImportExternalRepBaseDocumentXmlAsync(
        IFormFile? file,
        ImportExternalRepBaseDocumentFromXmlService service,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return TypedResults.BadRequest(new ExternalRepBaseDocumentImportResponse
            {
                Outcome = ImportExternalRepBaseDocumentFromXmlOutcome.Rejected.ToString(),
                IsSuccess = false,
                ValidationStatus = "Rejected",
                ReasonCode = ExternalRepBaseDocumentImportReasonCode.InvalidXml.ToString(),
                ReasonMessage = "El archivo XML es obligatorio."
            });
        }

        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);

        var result = await service.ExecuteAsync(
            new ImportExternalRepBaseDocumentFromXmlCommand
            {
                SourceFileName = file.FileName,
                FileContent = buffer.ToArray()
            },
            cancellationToken);

        var response = new ExternalRepBaseDocumentImportResponse
        {
            Outcome = result.Outcome.ToString(),
            IsSuccess = result.IsSuccess,
            ExternalRepBaseDocumentId = result.ExternalRepBaseDocumentId,
            ValidationStatus = result.ValidationStatus,
            ReasonCode = result.ReasonCode,
            ReasonMessage = result.ReasonMessage,
            ErrorMessage = result.ErrorMessage,
            Uuid = result.Uuid,
            IssuerRfc = result.IssuerRfc,
            ReceiverRfc = result.ReceiverRfc,
            PaymentMethodSat = result.PaymentMethodSat,
            PaymentFormSat = result.PaymentFormSat,
            CurrencyCode = result.CurrencyCode,
            Total = result.Total,
            IsDuplicate = result.IsDuplicate
        };

        await AuditApiHelper.RecordAsync(
            auditService,
            "ExternalRepBaseDocument.ImportXml",
            "ExternalRepBaseDocument",
            result.ExternalRepBaseDocumentId?.ToString(),
            result.Outcome.ToString(),
            new { file.FileName, file.Length, file.ContentType },
            new { result.ExternalRepBaseDocumentId, result.Uuid, result.ReasonCode, result.ValidationStatus, result.IsDuplicate },
            result.ErrorMessage ?? result.ReasonMessage,
            cancellationToken);

        return result.Outcome switch
        {
            ImportExternalRepBaseDocumentFromXmlOutcome.Accepted => TypedResults.Ok(response),
            ImportExternalRepBaseDocumentFromXmlOutcome.Blocked => TypedResults.Ok(response),
            ImportExternalRepBaseDocumentFromXmlOutcome.Duplicate => TypedResults.Conflict(response),
            _ => TypedResults.BadRequest(response)
        };
    }

    private static async Task<Results<Ok<ExternalRepBaseDocumentDetailResponse>, NotFound>> GetExternalRepBaseDocumentByIdAsync(
        long externalRepBaseDocumentId,
        GetExternalRepBaseDocumentByIdService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(externalRepBaseDocumentId, cancellationToken);
        if (result.Outcome == GetExternalRepBaseDocumentByIdOutcome.NotFound || result.Document is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(MapExternalRepBaseDocument(result.Document));
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
            Eligibility = MapEligibilityExplanation(item.Eligibility),
            RegisteredPaymentCount = item.RegisteredPaymentCount,
            PaymentComplementCount = item.PaymentComplementCount,
            StampedPaymentComplementCount = item.StampedPaymentComplementCount,
            LastRepIssuedAtUtc = item.LastRepIssuedAtUtc,
            OperationalState = item.OperationalState is null ? null : MapInternalRepOperationalState(item.OperationalState)
        };
    }

    public static ExternalRepBaseDocumentDetailResponse MapExternalRepBaseDocument(ExternalRepBaseDocument document)
    {
        var evaluation = ExternalRepBaseDocumentOperationalEvaluator.Evaluate(document);

        return new ExternalRepBaseDocumentDetailResponse
        {
            Id = document.Id,
            Uuid = document.Uuid,
            CfdiVersion = document.CfdiVersion,
            DocumentType = document.DocumentType,
            Series = document.Series,
            Folio = document.Folio,
            IssuedAtUtc = document.IssuedAtUtc,
            IssuerRfc = document.IssuerRfc,
            IssuerLegalName = document.IssuerLegalName,
            ReceiverRfc = document.ReceiverRfc,
            ReceiverLegalName = document.ReceiverLegalName,
            CurrencyCode = document.CurrencyCode,
            ExchangeRate = document.ExchangeRate,
            Subtotal = document.Subtotal,
            Total = document.Total,
            PaymentMethodSat = document.PaymentMethodSat,
            PaymentFormSat = document.PaymentFormSat,
            ValidationStatus = document.ValidationStatus.ToString(),
            ReasonCode = document.ValidationReasonCode,
            ReasonMessage = document.ValidationReasonMessage,
            SatStatus = document.SatStatus.ToString(),
            LastSatCheckAtUtc = document.LastSatCheckAtUtc,
            LastSatExternalStatus = document.LastSatExternalStatus,
            LastSatCancellationStatus = document.LastSatCancellationStatus,
            LastSatProviderCode = document.LastSatProviderCode,
            LastSatProviderMessage = document.LastSatProviderMessage,
            LastSatRawResponseSummaryJson = document.LastSatRawResponseSummaryJson,
            SourceFileName = document.SourceFileName,
            XmlHash = document.XmlHash,
            ImportedAtUtc = document.ImportedAtUtc,
            ImportedByUserId = document.ImportedByUserId,
            ImportedByUsername = document.ImportedByUsername,
            OperationalStatus = evaluation.Status.ToString(),
            IsEligible = evaluation.IsEligible,
            IsBlocked = evaluation.IsBlocked,
            PrimaryReasonCode = evaluation.PrimaryReasonCode,
            PrimaryReasonMessage = evaluation.PrimaryReasonMessage,
            AvailableActions = evaluation.AvailableActions.ToList()
        };
    }

    public static ExternalRepBaseDocumentItemResponse MapExternalRepBaseDocumentListItem(ExternalRepBaseDocumentListItem item)
    {
        return new ExternalRepBaseDocumentItemResponse
        {
            ExternalRepBaseDocumentId = item.ExternalRepBaseDocumentId,
            Uuid = item.Uuid,
            Series = item.Series,
            Folio = item.Folio,
            IssuedAtUtc = item.IssuedAtUtc,
            IssuerRfc = item.IssuerRfc,
            IssuerLegalName = item.IssuerLegalName,
            ReceiverRfc = item.ReceiverRfc,
            ReceiverLegalName = item.ReceiverLegalName,
            CurrencyCode = item.CurrencyCode,
            Total = item.Total,
            PaymentMethodSat = item.PaymentMethodSat,
            PaymentFormSat = item.PaymentFormSat,
            ValidationStatus = item.ValidationStatus,
            SatStatus = item.SatStatus,
            ImportedAtUtc = item.ImportedAtUtc,
            OperationalStatus = item.OperationalStatus,
            IsEligible = item.IsEligible,
            IsBlocked = item.IsBlocked,
            PrimaryReasonCode = item.PrimaryReasonCode,
            PrimaryReasonMessage = item.PrimaryReasonMessage,
            AvailableActions = item.AvailableActions.ToList()
        };
    }

    public static RepBaseDocumentItemResponse MapRepBaseDocument(RepBaseDocumentUnifiedListItem item)
    {
        return new RepBaseDocumentItemResponse
        {
            SourceType = item.SourceType,
            SourceId = item.SourceId,
            FiscalDocumentId = item.FiscalDocumentId,
            ExternalRepBaseDocumentId = item.ExternalRepBaseDocumentId,
            BillingDocumentId = item.BillingDocumentId,
            Uuid = item.Uuid,
            Series = item.Series,
            Folio = item.Folio,
            IssuedAtUtc = item.IssuedAtUtc,
            IssuerRfc = item.IssuerRfc,
            IssuerLegalName = item.IssuerLegalName,
            ReceiverRfc = item.ReceiverRfc,
            ReceiverLegalName = item.ReceiverLegalName,
            CurrencyCode = item.CurrencyCode,
            Total = item.Total,
            PaymentMethodSat = item.PaymentMethodSat,
            PaymentFormSat = item.PaymentFormSat,
            OperationalStatus = item.OperationalStatus,
            ValidationStatus = item.ValidationStatus,
            SatStatus = item.SatStatus,
            OutstandingBalance = item.OutstandingBalance,
            RepCount = item.RepCount,
            IsEligible = item.IsEligible,
            IsBlocked = item.IsBlocked,
            PrimaryReasonCode = item.PrimaryReasonCode,
            PrimaryReasonMessage = item.PrimaryReasonMessage,
            AvailableActions = item.AvailableActions.ToList(),
            ImportedAtUtc = item.ImportedAtUtc
        };
    }

    private static InternalRepBaseDocumentEligibilityExplanationResponse MapEligibilityExplanation(InternalRepBaseDocumentEligibilityExplanation explanation)
    {
        return new InternalRepBaseDocumentEligibilityExplanationResponse
        {
            Status = explanation.Status,
            PrimaryReasonCode = explanation.PrimaryReasonCode,
            PrimaryReasonMessage = explanation.PrimaryReasonMessage,
            EvaluatedAtUtc = explanation.EvaluatedAtUtc,
            SecondarySignals = explanation.SecondarySignals
                .Select(x => new InternalRepBaseDocumentEligibilitySignalResponse
                {
                    Code = x.Code,
                    Severity = x.Severity,
                    Message = x.Message
                })
                .ToList()
        };
    }

    private static InternalRepBaseDocumentOperationalStateResponse MapInternalRepOperationalState(InternalRepBaseDocumentOperationalSnapshot snapshot)
    {
        return new InternalRepBaseDocumentOperationalStateResponse
        {
            LastEligibilityEvaluatedAtUtc = snapshot.LastEligibilityEvaluatedAtUtc,
            LastEligibilityStatus = snapshot.LastEligibilityStatus,
            LastPrimaryReasonCode = snapshot.LastPrimaryReasonCode,
            LastPrimaryReasonMessage = snapshot.LastPrimaryReasonMessage,
            RepPendingFlag = snapshot.RepPendingFlag,
            LastRepIssuedAtUtc = snapshot.LastRepIssuedAtUtc,
            RepCount = snapshot.RepCount,
            TotalPaidApplied = snapshot.TotalPaidApplied
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

    public InternalRepBaseDocumentEligibilityExplanationResponse Eligibility { get; set; } = new();

    public int RegisteredPaymentCount { get; set; }

    public int PaymentComplementCount { get; set; }

    public int StampedPaymentComplementCount { get; set; }

    public DateTime? LastRepIssuedAtUtc { get; set; }

    public InternalRepBaseDocumentOperationalStateResponse? OperationalState { get; set; }
}

public sealed class InternalRepBaseDocumentDetailResponse
{
    public InternalRepBaseDocumentItemResponse Summary { get; set; } = new();

    public InternalRepBaseDocumentOperationalStateResponse? OperationalState { get; set; }

    public List<InternalRepBaseDocumentPaymentHistoryResponse> PaymentHistory { get; set; } = [];

    public List<InternalRepBaseDocumentPaymentApplicationResponse> PaymentApplications { get; set; } = [];

    public List<InternalRepBaseDocumentPaymentComplementResponse> IssuedReps { get; set; } = [];
}

public sealed class InternalRepBaseDocumentEligibilityExplanationResponse
{
    public string Status { get; set; } = string.Empty;

    public string PrimaryReasonCode { get; set; } = string.Empty;

    public string PrimaryReasonMessage { get; set; } = string.Empty;

    public DateTime EvaluatedAtUtc { get; set; }

    public List<InternalRepBaseDocumentEligibilitySignalResponse> SecondarySignals { get; set; } = [];
}

public sealed class InternalRepBaseDocumentEligibilitySignalResponse
{
    public string Code { get; set; } = string.Empty;

    public string Severity { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}

public sealed class InternalRepBaseDocumentOperationalStateResponse
{
    public DateTime LastEligibilityEvaluatedAtUtc { get; set; }

    public string LastEligibilityStatus { get; set; } = string.Empty;

    public string LastPrimaryReasonCode { get; set; } = string.Empty;

    public string LastPrimaryReasonMessage { get; set; } = string.Empty;

    public bool RepPendingFlag { get; set; }

    public DateTime? LastRepIssuedAtUtc { get; set; }

    public int RepCount { get; set; }

    public decimal TotalPaidApplied { get; set; }
}

public sealed class InternalRepBaseDocumentPaymentHistoryResponse
{
    public long AccountsReceivablePaymentId { get; set; }

    public DateTime PaymentDateUtc { get; set; }

    public string PaymentFormSat { get; set; } = string.Empty;

    public decimal PaymentAmount { get; set; }

    public decimal AmountAppliedToDocument { get; set; }

    public decimal RemainingPaymentAmount { get; set; }

    public string? Reference { get; set; }

    public string? Notes { get; set; }

    public long? PaymentComplementId { get; set; }

    public string? PaymentComplementStatus { get; set; }

    public string? PaymentComplementUuid { get; set; }

    public DateTime CreatedAtUtc { get; set; }
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

    public string? Notes { get; set; }

    public decimal PaymentAmount { get; set; }

    public decimal RemainingPaymentAmount { get; set; }

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

    public DateTime? CancelledAtUtc { get; set; }

    public string? ProviderName { get; set; }

    public int InstallmentNumber { get; set; }

    public decimal PreviousBalance { get; set; }

    public decimal PaidAmount { get; set; }

    public decimal RemainingBalance { get; set; }
}

public sealed class RegisterInternalRepBaseDocumentPaymentRequest
{
    public DateOnly PaymentDate { get; set; }

    public string PaymentFormSat { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string? Reference { get; set; }

    public string? Notes { get; set; }
}

public sealed class RegisterInternalRepBaseDocumentPaymentResponse
{
    public string Outcome { get; set; } = string.Empty;

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public List<string> WarningMessages { get; set; } = [];

    public long FiscalDocumentId { get; set; }

    public long? AccountsReceivableInvoiceId { get; set; }

    public long? AccountsReceivablePaymentId { get; set; }

    public decimal AppliedAmount { get; set; }

    public decimal RemainingBalance { get; set; }

    public decimal RemainingPaymentAmount { get; set; }

    public string? RepOperationalStatus { get; set; }

    public bool? IsEligible { get; set; }

    public bool? IsBlocked { get; set; }

    public string? EligibilityReason { get; set; }

    public InternalRepBaseDocumentOperationalStateResponse? OperationalState { get; set; }

    public List<RegisterInternalRepBaseDocumentPaymentApplicationResponse> Applications { get; set; } = [];
}

public sealed class RegisterInternalRepBaseDocumentPaymentApplicationResponse
{
    public long ApplicationId { get; set; }

    public long AccountsReceivablePaymentId { get; set; }

    public long AccountsReceivableInvoiceId { get; set; }

    public int ApplicationSequence { get; set; }

    public decimal AppliedAmount { get; set; }

    public decimal PreviousBalance { get; set; }

    public decimal NewBalance { get; set; }
}

public sealed class PrepareInternalRepBaseDocumentPaymentComplementRequest
{
    public long? AccountsReceivablePaymentId { get; set; }
}

public sealed class PrepareInternalRepBaseDocumentPaymentComplementResponse
{
    public string Outcome { get; set; } = string.Empty;

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public List<string> WarningMessages { get; set; } = [];

    public long FiscalDocumentId { get; set; }

    public long? AccountsReceivablePaymentId { get; set; }

    public long? PaymentComplementDocumentId { get; set; }

    public string? Status { get; set; }

    public int RelatedDocumentCount { get; set; }

    public InternalRepBaseDocumentOperationalStateResponse? OperationalState { get; set; }
}

public sealed class StampInternalRepBaseDocumentPaymentComplementRequest
{
    public long? PaymentComplementDocumentId { get; set; }

    public bool RetryRejected { get; set; }
}

public sealed class StampInternalRepBaseDocumentPaymentComplementResponse
{
    public string Outcome { get; set; } = string.Empty;

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public List<string> WarningMessages { get; set; } = [];

    public long FiscalDocumentId { get; set; }

    public long? AccountsReceivablePaymentId { get; set; }

    public long? PaymentComplementDocumentId { get; set; }

    public string? Status { get; set; }

    public long? PaymentComplementStampId { get; set; }

    public string? StampUuid { get; set; }

    public DateTime? StampedAtUtc { get; set; }

    public bool XmlAvailable { get; set; }

    public InternalRepBaseDocumentOperationalStateResponse? OperationalState { get; set; }
}

public sealed class ExternalRepBaseDocumentImportResponse
{
    public string Outcome { get; set; } = string.Empty;

    public bool IsSuccess { get; set; }

    public long? ExternalRepBaseDocumentId { get; set; }

    public string ValidationStatus { get; set; } = string.Empty;

    public string ReasonCode { get; set; } = string.Empty;

    public string ReasonMessage { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public string? Uuid { get; set; }

    public string? IssuerRfc { get; set; }

    public string? ReceiverRfc { get; set; }

    public string? PaymentMethodSat { get; set; }

    public string? PaymentFormSat { get; set; }

    public string? CurrencyCode { get; set; }

    public decimal? Total { get; set; }

    public bool IsDuplicate { get; set; }
}

public sealed class ExternalRepBaseDocumentDetailResponse
{
    public long Id { get; set; }

    public string Uuid { get; set; } = string.Empty;

    public string CfdiVersion { get; set; } = string.Empty;

    public string DocumentType { get; set; } = string.Empty;

    public string Series { get; set; } = string.Empty;

    public string Folio { get; set; } = string.Empty;

    public DateTime IssuedAtUtc { get; set; }

    public string IssuerRfc { get; set; } = string.Empty;

    public string? IssuerLegalName { get; set; }

    public string ReceiverRfc { get; set; } = string.Empty;

    public string? ReceiverLegalName { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public decimal ExchangeRate { get; set; }

    public decimal Subtotal { get; set; }

    public decimal Total { get; set; }

    public string PaymentMethodSat { get; set; } = string.Empty;

    public string PaymentFormSat { get; set; } = string.Empty;

    public string ValidationStatus { get; set; } = string.Empty;

    public string ReasonCode { get; set; } = string.Empty;

    public string ReasonMessage { get; set; } = string.Empty;

    public string SatStatus { get; set; } = string.Empty;

    public DateTime? LastSatCheckAtUtc { get; set; }

    public string? LastSatExternalStatus { get; set; }

    public string? LastSatCancellationStatus { get; set; }

    public string? LastSatProviderCode { get; set; }

    public string? LastSatProviderMessage { get; set; }

    public string? LastSatRawResponseSummaryJson { get; set; }

    public string SourceFileName { get; set; } = string.Empty;

    public string XmlHash { get; set; } = string.Empty;

    public DateTime ImportedAtUtc { get; set; }

    public long? ImportedByUserId { get; set; }

    public string? ImportedByUsername { get; set; }

    public string OperationalStatus { get; set; } = string.Empty;

    public bool IsEligible { get; set; }

    public bool IsBlocked { get; set; }

    public string PrimaryReasonCode { get; set; } = string.Empty;

    public string PrimaryReasonMessage { get; set; } = string.Empty;

    public List<string> AvailableActions { get; set; } = [];
}

public sealed class ExternalRepBaseDocumentListResponse
{
    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }

    public List<ExternalRepBaseDocumentItemResponse> Items { get; set; } = [];
}

public sealed class ExternalRepBaseDocumentItemResponse
{
    public long ExternalRepBaseDocumentId { get; set; }

    public string Uuid { get; set; } = string.Empty;

    public string Series { get; set; } = string.Empty;

    public string Folio { get; set; } = string.Empty;

    public DateTime IssuedAtUtc { get; set; }

    public string IssuerRfc { get; set; } = string.Empty;

    public string? IssuerLegalName { get; set; }

    public string ReceiverRfc { get; set; } = string.Empty;

    public string? ReceiverLegalName { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public decimal Total { get; set; }

    public string PaymentMethodSat { get; set; } = string.Empty;

    public string PaymentFormSat { get; set; } = string.Empty;

    public string ValidationStatus { get; set; } = string.Empty;

    public string SatStatus { get; set; } = string.Empty;

    public DateTime ImportedAtUtc { get; set; }

    public string OperationalStatus { get; set; } = string.Empty;

    public bool IsEligible { get; set; }

    public bool IsBlocked { get; set; }

    public string PrimaryReasonCode { get; set; } = string.Empty;

    public string PrimaryReasonMessage { get; set; } = string.Empty;

    public List<string> AvailableActions { get; set; } = [];
}

public sealed class RepBaseDocumentListResponse
{
    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }

    public List<RepBaseDocumentItemResponse> Items { get; set; } = [];
}

public sealed class RepBaseDocumentItemResponse
{
    public string SourceType { get; set; } = string.Empty;

    public long SourceId { get; set; }

    public long? FiscalDocumentId { get; set; }

    public long? ExternalRepBaseDocumentId { get; set; }

    public long? BillingDocumentId { get; set; }

    public string? Uuid { get; set; }

    public string Series { get; set; } = string.Empty;

    public string Folio { get; set; } = string.Empty;

    public DateTime IssuedAtUtc { get; set; }

    public string? IssuerRfc { get; set; }

    public string? IssuerLegalName { get; set; }

    public string ReceiverRfc { get; set; } = string.Empty;

    public string ReceiverLegalName { get; set; } = string.Empty;

    public string CurrencyCode { get; set; } = string.Empty;

    public decimal Total { get; set; }

    public string PaymentMethodSat { get; set; } = string.Empty;

    public string PaymentFormSat { get; set; } = string.Empty;

    public string OperationalStatus { get; set; } = string.Empty;

    public string? ValidationStatus { get; set; }

    public string? SatStatus { get; set; }

    public decimal? OutstandingBalance { get; set; }

    public int? RepCount { get; set; }

    public bool IsEligible { get; set; }

    public bool IsBlocked { get; set; }

    public string PrimaryReasonCode { get; set; } = string.Empty;

    public string PrimaryReasonMessage { get; set; } = string.Empty;

    public List<string> AvailableActions { get; set; } = [];

    public DateTime? ImportedAtUtc { get; set; }
}
