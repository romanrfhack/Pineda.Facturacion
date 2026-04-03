using Microsoft.AspNetCore.Http.HttpResults;
using Pineda.Facturacion.Api.Security;
using Pineda.Facturacion.Application.Abstractions.Security;
using Pineda.Facturacion.Application.Security;
using Pineda.Facturacion.Application.UseCases.AccountsReceivable;
using Pineda.Facturacion.Application.UseCases.PaymentComplements;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Api.Endpoints;

public static class AccountsReceivableEndpoints
{
    public static IEndpointRouteBuilder MapAccountsReceivableEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapPost("/api/fiscal-documents/{fiscalDocumentId:long}/accounts-receivable", CreateAccountsReceivableInvoiceAsync)
            .RequireAuthorization(AuthorizationPolicyNames.OperatorOrAbove)
            .WithTags("AccountsReceivable")
            .WithName("CreateAccountsReceivableInvoiceFromFiscalDocument")
            .WithSummary("Create an accounts receivable invoice from a stamped fiscal document")
            .Produces<CreateAccountsReceivableInvoiceResponse>(StatusCodes.Status200OK)
            .Produces<CreateAccountsReceivableInvoiceResponse>(StatusCodes.Status400BadRequest)
            .Produces<CreateAccountsReceivableInvoiceResponse>(StatusCodes.Status404NotFound)
            .Produces<CreateAccountsReceivableInvoiceResponse>(StatusCodes.Status409Conflict);

        endpoints.MapGet("/api/fiscal-documents/{fiscalDocumentId:long}/accounts-receivable", GetAccountsReceivableInvoiceByFiscalDocumentIdAsync)
            .RequireAuthorization(AuthorizationPolicyNames.Authenticated)
            .WithTags("AccountsReceivable")
            .WithName("GetAccountsReceivableInvoiceByFiscalDocumentId")
            .WithSummary("Get the accounts receivable invoice for a fiscal document")
            .Produces<AccountsReceivableInvoiceResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        endpoints.MapPost("/api/fiscal-documents/{fiscalDocumentId:long}/accounts-receivable/ensure", EnsureAccountsReceivableInvoiceAsync)
            .RequireAuthorization(AuthorizationPolicyNames.OperatorOrAbove)
            .WithTags("AccountsReceivable")
            .WithName("EnsureAccountsReceivableInvoiceForFiscalDocument")
            .WithSummary("Ensure an operational accounts receivable invoice exists for a fiscal document")
            .Produces<CreateAccountsReceivableInvoiceResponse>(StatusCodes.Status200OK)
            .Produces<CreateAccountsReceivableInvoiceResponse>(StatusCodes.Status400BadRequest)
            .Produces<CreateAccountsReceivableInvoiceResponse>(StatusCodes.Status404NotFound);

        var group = endpoints.MapGroup("/api/accounts-receivable")
            .WithTags("AccountsReceivable")
            .RequireAuthorization(AuthorizationPolicyNames.Authenticated);

        group.MapPost("/payments", CreateAccountsReceivablePaymentAsync)
            .RequireAuthorization(AuthorizationPolicyNames.OperatorOrAbove)
            .WithName("CreateAccountsReceivablePayment")
            .WithSummary("Create an accounts receivable payment event")
            .Produces<CreateAccountsReceivablePaymentResponse>(StatusCodes.Status200OK)
            .Produces<CreateAccountsReceivablePaymentResponse>(StatusCodes.Status400BadRequest);

        group.MapGet("/invoices", SearchAccountsReceivablePortfolioAsync)
            .WithName("SearchAccountsReceivablePortfolio")
            .WithSummary("List minimal accounts receivable portfolio rows")
            .Produces<AccountsReceivablePortfolioResponse>(StatusCodes.Status200OK);

        group.MapGet("/receivers/{fiscalReceiverId:long}/workspace", GetAccountsReceivableReceiverWorkspaceAsync)
            .WithName("GetAccountsReceivableReceiverWorkspace")
            .WithSummary("Get a lightweight accounts receivable workspace for a fiscal receiver")
            .Produces<AccountsReceivableReceiverWorkspaceResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/invoices/{accountsReceivableInvoiceId:long}", GetAccountsReceivableInvoiceByIdAsync)
            .WithName("GetAccountsReceivableInvoiceById")
            .WithSummary("Get the consolidated detail for an accounts receivable invoice")
            .Produces<AccountsReceivableInvoiceResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/invoices/{accountsReceivableInvoiceId:long}/collection-commitments", ListCollectionCommitmentsAsync)
            .WithName("ListCollectionCommitments")
            .WithSummary("List collection commitments for an accounts receivable invoice")
            .Produces<CollectionCommitmentsResponse>(StatusCodes.Status200OK);

        group.MapPost("/invoices/{accountsReceivableInvoiceId:long}/collection-commitments", CreateCollectionCommitmentAsync)
            .RequireAuthorization(AuthorizationPolicyNames.OperatorOrAbove)
            .WithName("CreateCollectionCommitment")
            .WithSummary("Create a collection commitment for an accounts receivable invoice")
            .Produces<CreateCollectionCommitmentResponse>(StatusCodes.Status200OK)
            .Produces<CreateCollectionCommitmentResponse>(StatusCodes.Status400BadRequest)
            .Produces<CreateCollectionCommitmentResponse>(StatusCodes.Status404NotFound)
            .Produces<CreateCollectionCommitmentResponse>(StatusCodes.Status409Conflict);

        group.MapGet("/invoices/{accountsReceivableInvoiceId:long}/collection-notes", ListCollectionNotesAsync)
            .WithName("ListCollectionNotes")
            .WithSummary("List collection notes for an accounts receivable invoice")
            .Produces<CollectionNotesResponse>(StatusCodes.Status200OK);

        group.MapPost("/invoices/{accountsReceivableInvoiceId:long}/collection-notes", CreateCollectionNoteAsync)
            .RequireAuthorization(AuthorizationPolicyNames.OperatorOrAbove)
            .WithName("CreateCollectionNote")
            .WithSummary("Create a collection note for an accounts receivable invoice")
            .Produces<CreateCollectionNoteResponse>(StatusCodes.Status200OK)
            .Produces<CreateCollectionNoteResponse>(StatusCodes.Status400BadRequest)
            .Produces<CreateCollectionNoteResponse>(StatusCodes.Status404NotFound)
            .Produces<CreateCollectionNoteResponse>(StatusCodes.Status409Conflict);

        group.MapGet("/payments", SearchAccountsReceivablePaymentsAsync)
            .WithName("SearchAccountsReceivablePayments")
            .WithSummary("List minimal accounts receivable payment rows with operational status")
            .Produces<AccountsReceivablePaymentsResponse>(StatusCodes.Status200OK);

        group.MapGet("/payments/{paymentId:long}", GetAccountsReceivablePaymentByIdAsync)
            .WithName("GetAccountsReceivablePaymentById")
            .WithSummary("Get an accounts receivable payment and its applications")
            .Produces<AccountsReceivablePaymentResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/payments/{paymentId:long}/apply", ApplyAccountsReceivablePaymentAsync)
            .RequireAuthorization(AuthorizationPolicyNames.OperatorOrAbove)
            .WithName("ApplyAccountsReceivablePayment")
            .WithSummary("Apply a payment across one or more accounts receivable invoices")
            .Produces<ApplyAccountsReceivablePaymentResponse>(StatusCodes.Status200OK)
            .Produces<ApplyAccountsReceivablePaymentResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApplyAccountsReceivablePaymentResponse>(StatusCodes.Status404NotFound)
            .Produces<ApplyAccountsReceivablePaymentResponse>(StatusCodes.Status409Conflict);

        group.MapPost("/payments/{paymentId:long}/payment-complements", PreparePaymentComplementAsync)
            .RequireAuthorization(AuthorizationPolicyNames.OperatorOrAbove)
            .WithName("PreparePaymentComplement")
            .WithSummary("Prepare a persisted payment complement snapshot from a payment event and its applications")
            .Produces<PreparePaymentComplementResponse>(StatusCodes.Status200OK)
            .Produces<PreparePaymentComplementResponse>(StatusCodes.Status400BadRequest)
            .Produces<PreparePaymentComplementResponse>(StatusCodes.Status404NotFound)
            .Produces<PreparePaymentComplementResponse>(StatusCodes.Status409Conflict);

        group.MapGet("/payments/{paymentId:long}/payment-complement", GetPaymentComplementByPaymentIdAsync)
            .WithName("GetPaymentComplementByPaymentId")
            .WithSummary("Get the persisted payment complement snapshot for a payment event")
            .Produces<PaymentComplementDocumentResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<Results<Ok<CreateAccountsReceivableInvoiceResponse>, BadRequest<CreateAccountsReceivableInvoiceResponse>, NotFound<CreateAccountsReceivableInvoiceResponse>, Conflict<CreateAccountsReceivableInvoiceResponse>>> CreateAccountsReceivableInvoiceAsync(
        long fiscalDocumentId,
        CreateAccountsReceivableInvoiceRequest? request,
        CreateAccountsReceivableInvoiceFromFiscalDocumentService service,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(
            new CreateAccountsReceivableInvoiceFromFiscalDocumentCommand
            {
                FiscalDocumentId = fiscalDocumentId,
                OverrideCreditDays = request?.OverrideCreditDays
            },
            cancellationToken);

        var response = new CreateAccountsReceivableInvoiceResponse
        {
            Outcome = result.Outcome.ToString(),
            IsSuccess = result.IsSuccess,
            ErrorMessage = result.ErrorMessage,
            FiscalDocumentId = result.FiscalDocumentId,
            AccountsReceivableInvoice = result.AccountsReceivableInvoice is null
                ? null
                : MapInvoice(result.AccountsReceivableInvoice)
        };

        await AuditApiHelper.RecordAsync(
            auditService,
            "AccountsReceivableInvoice.Create",
            "AccountsReceivableInvoice",
            result.AccountsReceivableInvoice?.Id.ToString() ?? fiscalDocumentId.ToString(),
            result.Outcome.ToString(),
            new { fiscalDocumentId, request?.OverrideCreditDays },
            new { invoiceId = result.AccountsReceivableInvoice?.Id, result.AccountsReceivableInvoice?.Status, result.AccountsReceivableInvoice?.OutstandingBalance },
            result.ErrorMessage,
            cancellationToken);

        return result.Outcome switch
        {
            CreateAccountsReceivableInvoiceFromFiscalDocumentOutcome.Created => TypedResults.Ok(response),
            CreateAccountsReceivableInvoiceFromFiscalDocumentOutcome.NotFound => TypedResults.NotFound(response),
            CreateAccountsReceivableInvoiceFromFiscalDocumentOutcome.Conflict => TypedResults.Conflict(response),
            _ => TypedResults.BadRequest(response)
        };
    }

    private static async Task<Results<Ok<AccountsReceivableInvoiceResponse>, NotFound>> GetAccountsReceivableInvoiceByFiscalDocumentIdAsync(
        long fiscalDocumentId,
        GetAccountsReceivableInvoiceDetailService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteByFiscalDocumentIdAsync(fiscalDocumentId, cancellationToken);
        if (result.Outcome == GetAccountsReceivableInvoiceDetailOutcome.NotFound || result.Detail is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(MapInvoice(result.Detail));
    }

    private static async Task<Results<Ok<AccountsReceivableInvoiceResponse>, NotFound>> GetAccountsReceivableInvoiceByIdAsync(
        long accountsReceivableInvoiceId,
        GetAccountsReceivableInvoiceDetailService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteByInvoiceIdAsync(accountsReceivableInvoiceId, cancellationToken);
        if (result.Outcome == GetAccountsReceivableInvoiceDetailOutcome.NotFound || result.Detail is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(MapInvoice(result.Detail));
    }

    private static async Task<Results<Ok<CreateAccountsReceivableInvoiceResponse>, BadRequest<CreateAccountsReceivableInvoiceResponse>, NotFound<CreateAccountsReceivableInvoiceResponse>>> EnsureAccountsReceivableInvoiceAsync(
        long fiscalDocumentId,
        EnsureAccountsReceivableInvoiceForFiscalDocumentService service,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(
            new EnsureAccountsReceivableInvoiceForFiscalDocumentCommand
            {
                FiscalDocumentId = fiscalDocumentId
            },
            cancellationToken);

        var response = new CreateAccountsReceivableInvoiceResponse
        {
            Outcome = result.Outcome.ToString(),
            IsSuccess = result.IsSuccess,
            ErrorMessage = result.ErrorMessage,
            FiscalDocumentId = result.FiscalDocumentId,
            AccountsReceivableInvoice = result.AccountsReceivableInvoice is null
                ? null
                : MapInvoice(result.AccountsReceivableInvoice)
        };

        await AuditApiHelper.RecordAsync(
            auditService,
            "AccountsReceivableInvoice.Ensure",
            "AccountsReceivableInvoice",
            result.AccountsReceivableInvoice?.Id.ToString() ?? fiscalDocumentId.ToString(),
            result.Outcome.ToString(),
            new { fiscalDocumentId },
            new { invoiceId = result.AccountsReceivableInvoice?.Id, result.AccountsReceivableInvoice?.Status, result.AccountsReceivableInvoice?.OutstandingBalance },
            result.ErrorMessage,
            cancellationToken);

        return result.Outcome switch
        {
            EnsureAccountsReceivableInvoiceForFiscalDocumentOutcome.NotFound => TypedResults.NotFound(response),
            EnsureAccountsReceivableInvoiceForFiscalDocumentOutcome.ValidationFailed => TypedResults.BadRequest(response),
            _ => TypedResults.Ok(response)
        };
    }

    private static async Task<Results<Ok<CreateAccountsReceivablePaymentResponse>, BadRequest<CreateAccountsReceivablePaymentResponse>>> CreateAccountsReceivablePaymentAsync(
        CreateAccountsReceivablePaymentRequest request,
        CreateAccountsReceivablePaymentService service,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(
            new CreateAccountsReceivablePaymentCommand
            {
                PaymentDateUtc = request.PaymentDateUtc,
                PaymentFormSat = request.PaymentFormSat,
                Amount = request.Amount,
                Reference = request.Reference,
                Notes = request.Notes,
                ReceivedFromFiscalReceiverId = request.ReceivedFromFiscalReceiverId
            },
            cancellationToken);

        var response = new CreateAccountsReceivablePaymentResponse
        {
            Outcome = result.Outcome.ToString(),
            IsSuccess = result.IsSuccess,
            ErrorMessage = result.ErrorMessage,
            Payment = result.AccountsReceivablePayment is null
                ? null
                : MapPayment(result.AccountsReceivablePayment)
        };

        await AuditApiHelper.RecordAsync(
            auditService,
            "AccountsReceivablePayment.Create",
            "AccountsReceivablePayment",
            result.AccountsReceivablePayment?.Id.ToString(),
            result.Outcome.ToString(),
            new { request.PaymentDateUtc, request.PaymentFormSat, request.Amount, request.Reference },
            new { paymentId = result.AccountsReceivablePayment?.Id, result.AccountsReceivablePayment?.Amount },
            result.ErrorMessage,
            cancellationToken);

        return result.Outcome == CreateAccountsReceivablePaymentOutcome.Created
            ? TypedResults.Ok(response)
            : TypedResults.BadRequest(response);
    }

    private static async Task<Ok<AccountsReceivablePortfolioResponse>> SearchAccountsReceivablePortfolioAsync(
        long? fiscalReceiverId,
        string? receiverQuery,
        string? status,
        DateOnly? dueDateFrom,
        DateOnly? dueDateTo,
        bool? hasPendingBalance,
        bool? overdueOnly,
        bool? dueSoonOnly,
        bool? hasPendingCommitment,
        bool? followUpPending,
        SearchAccountsReceivablePortfolioService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(
            new SearchAccountsReceivablePortfolioFilter
            {
                FiscalReceiverId = fiscalReceiverId,
                ReceiverQuery = receiverQuery,
                Status = status,
                DueDateFromUtc = dueDateFrom?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                DueDateToUtcInclusive = dueDateTo?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                HasPendingBalance = hasPendingBalance,
                OverdueOnly = overdueOnly,
                DueSoonOnly = dueSoonOnly,
                HasPendingCommitment = hasPendingCommitment,
                FollowUpPending = followUpPending
            },
            cancellationToken);

        return TypedResults.Ok(new AccountsReceivablePortfolioResponse
        {
            Items = result.Items.Select(MapPortfolioItem).ToList()
        });
    }

    private static async Task<Results<Ok<AccountsReceivableReceiverWorkspaceResponse>, NotFound>> GetAccountsReceivableReceiverWorkspaceAsync(
        long fiscalReceiverId,
        GetAccountsReceivableReceiverWorkspaceService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(fiscalReceiverId, cancellationToken);
        if (result.Outcome == GetAccountsReceivableReceiverWorkspaceOutcome.NotFound || result.Workspace is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(MapReceiverWorkspace(result.Workspace));
    }

    private static async Task<Ok<CollectionCommitmentsResponse>> ListCollectionCommitmentsAsync(
        long accountsReceivableInvoiceId,
        ListCollectionCommitmentsByInvoiceIdService service,
        CancellationToken cancellationToken)
    {
        var items = await service.ExecuteAsync(accountsReceivableInvoiceId, cancellationToken);
        return TypedResults.Ok(new CollectionCommitmentsResponse
        {
            Items = items.Select(MapCommitment).ToList()
        });
    }

    private static async Task<Results<Ok<CreateCollectionCommitmentResponse>, BadRequest<CreateCollectionCommitmentResponse>, NotFound<CreateCollectionCommitmentResponse>, Conflict<CreateCollectionCommitmentResponse>>> CreateCollectionCommitmentAsync(
        long accountsReceivableInvoiceId,
        CreateCollectionCommitmentRequest request,
        CreateCollectionCommitmentService service,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(new CreateCollectionCommitmentCommand
        {
            AccountsReceivableInvoiceId = accountsReceivableInvoiceId,
            PromisedAmount = request.PromisedAmount,
            PromisedDateUtc = request.PromisedDateUtc,
            Notes = request.Notes
        }, cancellationToken);

        var response = new CreateCollectionCommitmentResponse
        {
            Outcome = result.Outcome.ToString(),
            IsSuccess = result.IsSuccess,
            ErrorMessage = result.ErrorMessage,
            Commitment = result.Commitment is null ? null : MapCommitment(result.Commitment)
        };

        await AuditApiHelper.RecordAsync(
            auditService,
            "CollectionCommitment.Create",
            "CollectionCommitment",
            response.Commitment?.Id.ToString() ?? accountsReceivableInvoiceId.ToString(),
            result.Outcome.ToString(),
            new { accountsReceivableInvoiceId, request.PromisedAmount, request.PromisedDateUtc },
            new { response.Commitment?.Status },
            result.ErrorMessage,
            cancellationToken);

        return result.Outcome switch
        {
            CreateCollectionCommitmentOutcome.Created => TypedResults.Ok(response),
            CreateCollectionCommitmentOutcome.NotFound => TypedResults.NotFound(response),
            CreateCollectionCommitmentOutcome.Conflict => TypedResults.Conflict(response),
            _ => TypedResults.BadRequest(response)
        };
    }

    private static async Task<Ok<CollectionNotesResponse>> ListCollectionNotesAsync(
        long accountsReceivableInvoiceId,
        ListCollectionNotesByInvoiceIdService service,
        CancellationToken cancellationToken)
    {
        var items = await service.ExecuteAsync(accountsReceivableInvoiceId, cancellationToken);
        return TypedResults.Ok(new CollectionNotesResponse
        {
            Items = items.Select(MapNote).ToList()
        });
    }

    private static async Task<Results<Ok<CreateCollectionNoteResponse>, BadRequest<CreateCollectionNoteResponse>, NotFound<CreateCollectionNoteResponse>, Conflict<CreateCollectionNoteResponse>>> CreateCollectionNoteAsync(
        long accountsReceivableInvoiceId,
        CreateCollectionNoteRequest request,
        CreateCollectionNoteService service,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(new CreateCollectionNoteCommand
        {
            AccountsReceivableInvoiceId = accountsReceivableInvoiceId,
            NoteType = request.NoteType,
            Content = request.Content,
            NextFollowUpAtUtc = request.NextFollowUpAtUtc
        }, cancellationToken);

        var response = new CreateCollectionNoteResponse
        {
            Outcome = result.Outcome.ToString(),
            IsSuccess = result.IsSuccess,
            ErrorMessage = result.ErrorMessage,
            Note = result.Note is null ? null : MapNote(result.Note)
        };

        await AuditApiHelper.RecordAsync(
            auditService,
            "CollectionNote.Create",
            "CollectionNote",
            response.Note?.Id.ToString() ?? accountsReceivableInvoiceId.ToString(),
            result.Outcome.ToString(),
            new { accountsReceivableInvoiceId, request.NoteType, request.NextFollowUpAtUtc },
            new { response.Note?.NoteType },
            result.ErrorMessage,
            cancellationToken);

        return result.Outcome switch
        {
            CreateCollectionNoteOutcome.Created => TypedResults.Ok(response),
            CreateCollectionNoteOutcome.NotFound => TypedResults.NotFound(response),
            CreateCollectionNoteOutcome.Conflict => TypedResults.Conflict(response),
            _ => TypedResults.BadRequest(response)
        };
    }

    private static async Task<Results<Ok<AccountsReceivablePaymentResponse>, NotFound>> GetAccountsReceivablePaymentByIdAsync(
        long paymentId,
        GetAccountsReceivablePaymentByIdService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(paymentId, cancellationToken);
        if (result.Outcome == GetAccountsReceivablePaymentByIdOutcome.NotFound || result.AccountsReceivablePayment is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(MapPayment(result.AccountsReceivablePayment, result.OperationalProjection));
    }

    private static async Task<Ok<AccountsReceivablePaymentsResponse>> SearchAccountsReceivablePaymentsAsync(
        long? fiscalReceiverId,
        string? operationalStatus,
        DateOnly? receivedFrom,
        DateOnly? receivedTo,
        bool? hasUnappliedAmount,
        long? linkedFiscalDocumentId,
        SearchAccountsReceivablePaymentsService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(
            new SearchAccountsReceivablePaymentsFilter
            {
                FiscalReceiverId = fiscalReceiverId,
                OperationalStatus = operationalStatus,
                ReceivedFromUtc = receivedFrom?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                ReceivedToUtcInclusive = receivedTo?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                HasUnappliedAmount = hasUnappliedAmount,
                LinkedFiscalDocumentId = linkedFiscalDocumentId
            },
            cancellationToken);

        return TypedResults.Ok(new AccountsReceivablePaymentsResponse
        {
            Items = result.Items.Select(MapPaymentProjection).ToList()
        });
    }

    private static async Task<Results<Ok<ApplyAccountsReceivablePaymentResponse>, BadRequest<ApplyAccountsReceivablePaymentResponse>, NotFound<ApplyAccountsReceivablePaymentResponse>, Conflict<ApplyAccountsReceivablePaymentResponse>>> ApplyAccountsReceivablePaymentAsync(
        long paymentId,
        ApplyAccountsReceivablePaymentRequest request,
        ApplyAccountsReceivablePaymentService service,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(
            new ApplyAccountsReceivablePaymentCommand
            {
                AccountsReceivablePaymentId = paymentId,
                Applications = request.Applications.Select(x => new ApplyAccountsReceivablePaymentApplicationInput
                {
                    AccountsReceivableInvoiceId = x.AccountsReceivableInvoiceId,
                    AppliedAmount = x.AppliedAmount
                }).ToList()
            },
            cancellationToken);

        var response = new ApplyAccountsReceivablePaymentResponse
        {
            Outcome = result.Outcome.ToString(),
            IsSuccess = result.IsSuccess,
            ErrorMessage = result.ErrorMessage,
            AccountsReceivablePaymentId = result.AccountsReceivablePaymentId,
            AppliedCount = result.AppliedCount,
            RemainingPaymentAmount = result.RemainingPaymentAmount,
            Applications = result.Applications
                .OrderBy(x => x.ApplicationSequence)
                .Select(MapApplication)
                .ToList(),
            Payment = result.AccountsReceivablePayment is null
                ? null
                : MapPayment(result.AccountsReceivablePayment)
        };

        await AuditApiHelper.RecordAsync(
            auditService,
            "AccountsReceivablePayment.Apply",
            "AccountsReceivablePayment",
            paymentId.ToString(),
            result.Outcome.ToString(),
            new
            {
                paymentId,
                applications = request.Applications.Select(x => new { x.AccountsReceivableInvoiceId, x.AppliedAmount }).ToList()
            },
            new { result.AppliedCount, result.RemainingPaymentAmount },
            result.ErrorMessage,
            cancellationToken);

        return result.Outcome switch
        {
            ApplyAccountsReceivablePaymentOutcome.Applied => TypedResults.Ok(response),
            ApplyAccountsReceivablePaymentOutcome.NotFound => TypedResults.NotFound(response),
            ApplyAccountsReceivablePaymentOutcome.Conflict => TypedResults.Conflict(response),
            _ => TypedResults.BadRequest(response)
        };
    }

    private static async Task<Results<Ok<PreparePaymentComplementResponse>, BadRequest<PreparePaymentComplementResponse>, NotFound<PreparePaymentComplementResponse>, Conflict<PreparePaymentComplementResponse>>> PreparePaymentComplementAsync(
        long paymentId,
        PreparePaymentComplementRequest? request,
        PreparePaymentComplementService service,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(
            new PreparePaymentComplementCommand
            {
                AccountsReceivablePaymentId = paymentId,
                IssuedAtUtc = request?.IssuedAtUtc
            },
            cancellationToken);

        var response = new PreparePaymentComplementResponse
        {
            Outcome = result.Outcome.ToString(),
            IsSuccess = result.IsSuccess,
            ErrorMessage = result.ErrorMessage,
            AccountsReceivablePaymentId = result.AccountsReceivablePaymentId,
            PaymentComplementId = result.PaymentComplementId,
            Status = result.Status?.ToString()
        };

        await AuditApiHelper.RecordAsync(
            auditService,
            "PaymentComplement.Prepare",
            "PaymentComplementDocument",
            result.PaymentComplementId?.ToString() ?? paymentId.ToString(),
            result.Outcome.ToString(),
            new { paymentId, request?.IssuedAtUtc },
            new { result.PaymentComplementId, result.Status },
            result.ErrorMessage,
            cancellationToken);

        return result.Outcome switch
        {
            PreparePaymentComplementOutcome.Created => TypedResults.Ok(response),
            PreparePaymentComplementOutcome.NotFound => TypedResults.NotFound(response),
            PreparePaymentComplementOutcome.Conflict => TypedResults.Conflict(response),
            _ => TypedResults.BadRequest(response)
        };
    }

    private static async Task<Results<Ok<PaymentComplementDocumentResponse>, NotFound>> GetPaymentComplementByPaymentIdAsync(
        long paymentId,
        GetPaymentComplementByPaymentIdService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(paymentId, cancellationToken);
        if (result.Outcome == GetPaymentComplementByPaymentIdOutcome.NotFound || result.PaymentComplementDocument is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(PaymentComplementsEndpoints.MapPaymentComplement(result.PaymentComplementDocument));
    }

    private static AccountsReceivableInvoiceResponse MapInvoice(
        AccountsReceivableInvoice invoice,
        IReadOnlyList<CollectionCommitmentProjection>? commitments = null,
        IReadOnlyList<CollectionNoteProjection>? notes = null)
    {
        commitments ??= [];
        notes ??= [];
        var collectionSummary = AccountsReceivableCollectionProjectionBuilder.BuildSummary(
            invoice.OutstandingBalance,
            invoice.Status.ToString(),
            invoice.DueAtUtc,
            commitments,
            notes,
            DateTime.UtcNow);

        return new AccountsReceivableInvoiceResponse
        {
            Id = invoice.Id,
            BillingDocumentId = invoice.BillingDocumentId,
            FiscalDocumentId = invoice.FiscalDocumentId,
            FiscalStampId = invoice.FiscalStampId,
            FiscalReceiverId = invoice.FiscalReceiverId,
            Status = invoice.Status.ToString(),
            PaymentMethodSat = invoice.PaymentMethodSat,
            PaymentFormSatInitial = invoice.PaymentFormSatInitial,
            IsCreditSale = invoice.IsCreditSale,
            CreditDays = invoice.CreditDays,
            IssuedAtUtc = invoice.IssuedAtUtc,
            DueAtUtc = invoice.DueAtUtc,
            CurrencyCode = invoice.CurrencyCode,
            Total = invoice.Total,
            PaidTotal = invoice.PaidTotal,
            OutstandingBalance = invoice.OutstandingBalance,
            CreatedAtUtc = invoice.CreatedAtUtc,
            UpdatedAtUtc = invoice.UpdatedAtUtc,
            AgingBucket = collectionSummary.AgingBucket.ToString(),
            HasPendingCommitment = collectionSummary.HasPendingCommitment,
            NextCommitmentDateUtc = collectionSummary.NextCommitmentDateUtc,
            NextFollowUpAtUtc = collectionSummary.NextFollowUpAtUtc,
            FollowUpPending = collectionSummary.FollowUpPending,
            CollectionCommitments = commitments.Select(MapCommitment).ToList(),
            CollectionNotes = notes.Select(MapNote).ToList(),
            Applications = invoice.Applications
                .OrderBy(x => x.ApplicationSequence)
                .Select(MapApplication)
                .ToList()
        };
    }

    private static AccountsReceivableInvoiceResponse MapInvoice(AccountsReceivableInvoiceDetailProjection detail)
    {
        var response = MapInvoice(detail.Invoice, detail.Commitments, detail.Notes);
        response.ReceiverRfc = detail.ReceiverRfc;
        response.ReceiverLegalName = detail.ReceiverLegalName;
        response.FiscalSeries = detail.FiscalSeries;
        response.FiscalFolio = detail.FiscalFolio;
        response.FiscalUuid = detail.FiscalUuid;
        response.RelatedPayments = detail.RelatedPaymentEntities
            .Select(payment =>
            {
                var projection = detail.RelatedPayments.FirstOrDefault(x => x.PaymentId == payment.Id);
                return MapPayment(payment, projection);
            })
            .ToList();
        response.RelatedPaymentComplements = detail.RelatedPaymentComplements.Select(MapPaymentComplementSummary).ToList();
        response.Timeline = detail.Timeline.Select(MapTimelineEntry).ToList();
        return response;
    }

    private static AccountsReceivablePaymentResponse MapPayment(
        AccountsReceivablePayment payment,
        AccountsReceivablePaymentOperationalProjection? projection = null)
    {
        var applications = payment.Applications
            .OrderBy(x => x.ApplicationSequence)
            .Select(MapApplication)
            .ToList();
        var appliedTotal = payment.Applications.Sum(x => x.AppliedAmount);
        projection ??= new AccountsReceivablePaymentOperationalProjection
        {
            PaymentId = payment.Id,
            ReceivedAtUtc = payment.PaymentDateUtc,
            Amount = payment.Amount,
            AppliedAmount = appliedTotal,
            UnappliedAmount = payment.Amount - appliedTotal,
            CurrencyCode = payment.CurrencyCode,
            Reference = payment.Reference,
            FiscalReceiverId = payment.ReceivedFromFiscalReceiverId,
            OperationalStatus = payment.Applications.Count == 0
                ? AccountsReceivablePaymentOperationalStatus.CapturedUnapplied
                : appliedTotal < payment.Amount
                    ? AccountsReceivablePaymentOperationalStatus.PartiallyApplied
                    : AccountsReceivablePaymentOperationalStatus.FullyApplied,
            RepStatus = payment.Applications.Count == 0
                ? AccountsReceivablePaymentRepStatus.NoApplications
                : appliedTotal == payment.Amount
                    ? AccountsReceivablePaymentRepStatus.ReadyToPrepare
                    : AccountsReceivablePaymentRepStatus.PendingApplications,
            ApplicationsCount = payment.Applications.Count
        };

        return new AccountsReceivablePaymentResponse
        {
            Id = payment.Id,
            PaymentDateUtc = payment.PaymentDateUtc,
            PaymentFormSat = payment.PaymentFormSat,
            CurrencyCode = payment.CurrencyCode,
            Amount = payment.Amount,
            AppliedTotal = projection.AppliedAmount,
            RemainingAmount = projection.UnappliedAmount,
            Reference = payment.Reference,
            Notes = payment.Notes,
            ReceivedFromFiscalReceiverId = payment.ReceivedFromFiscalReceiverId,
            OperationalStatus = projection.OperationalStatus.ToString(),
            RepStatus = projection.RepStatus.ToString(),
            RepDocumentStatus = projection.RepDocumentStatus,
            RepReservedAmount = projection.RepReservedAmount,
            RepFiscalizedAmount = projection.RepFiscalizedAmount,
            ApplicationsCount = projection.ApplicationsCount,
            LinkedFiscalDocumentId = projection.LinkedFiscalDocumentId,
            CreatedAtUtc = payment.CreatedAtUtc,
            UpdatedAtUtc = payment.UpdatedAtUtc,
            Applications = applications
        };
    }

    private static AccountsReceivablePaymentApplicationResponse MapApplication(AccountsReceivablePaymentApplication application)
    {
        return new AccountsReceivablePaymentApplicationResponse
        {
            Id = application.Id,
            AccountsReceivablePaymentId = application.AccountsReceivablePaymentId,
            AccountsReceivableInvoiceId = application.AccountsReceivableInvoiceId,
            ApplicationSequence = application.ApplicationSequence,
            AppliedAmount = application.AppliedAmount,
            PreviousBalance = application.PreviousBalance,
            NewBalance = application.NewBalance,
            CreatedAtUtc = application.CreatedAtUtc
        };
    }

    private static AccountsReceivablePortfolioItemResponse MapPortfolioItem(AccountsReceivablePortfolioItem item)
    {
        return new AccountsReceivablePortfolioItemResponse
        {
            AccountsReceivableInvoiceId = item.AccountsReceivableInvoiceId,
            FiscalDocumentId = item.FiscalDocumentId,
            FiscalReceiverId = item.FiscalReceiverId,
            ReceiverRfc = item.ReceiverRfc,
            ReceiverLegalName = item.ReceiverLegalName,
            FiscalSeries = item.FiscalSeries,
            FiscalFolio = item.FiscalFolio,
            FiscalUuid = item.FiscalUuid,
            Total = item.Total,
            PaidTotal = item.PaidTotal,
            OutstandingBalance = item.OutstandingBalance,
            IssuedAtUtc = item.IssuedAtUtc,
            DueAtUtc = item.DueAtUtc,
            Status = item.Status,
            DaysPastDue = item.DaysPastDue,
            AgingBucket = item.AgingBucket,
            HasPendingCommitment = item.HasPendingCommitment,
            NextCommitmentDateUtc = item.NextCommitmentDateUtc,
            NextFollowUpAtUtc = item.NextFollowUpAtUtc,
            FollowUpPending = item.FollowUpPending
        };
    }

    private static CollectionCommitmentResponse MapCommitment(CollectionCommitmentProjection item)
    {
        return new CollectionCommitmentResponse
        {
            Id = item.Id,
            AccountsReceivableInvoiceId = item.AccountsReceivableInvoiceId,
            PromisedAmount = item.PromisedAmount,
            PromisedDateUtc = item.PromisedDateUtc,
            Status = item.Status,
            Notes = item.Notes,
            CreatedAtUtc = item.CreatedAtUtc,
            UpdatedAtUtc = item.UpdatedAtUtc,
            CreatedByUsername = item.CreatedByUsername
        };
    }

    private static CollectionNoteResponse MapNote(CollectionNoteProjection item)
    {
        return new CollectionNoteResponse
        {
            Id = item.Id,
            AccountsReceivableInvoiceId = item.AccountsReceivableInvoiceId,
            NoteType = item.NoteType,
            Content = item.Content,
            NextFollowUpAtUtc = item.NextFollowUpAtUtc,
            CreatedAtUtc = item.CreatedAtUtc,
            CreatedByUsername = item.CreatedByUsername
        };
    }

    private static AccountsReceivablePaymentComplementSummaryResponse MapPaymentComplementSummary(AccountsReceivableInvoiceRepSummary item)
    {
        return new AccountsReceivablePaymentComplementSummaryResponse
        {
            PaymentComplementId = item.PaymentComplementId,
            AccountsReceivablePaymentId = item.AccountsReceivablePaymentId,
            Status = item.Status,
            TotalPaymentsAmount = item.TotalPaymentsAmount,
            IssuedAtUtc = item.IssuedAtUtc,
            PaymentDateUtc = item.PaymentDateUtc,
            Uuid = item.Uuid,
            StampedAtUtc = item.StampedAtUtc,
            CancelledAtUtc = item.CancelledAtUtc
        };
    }

    private static AccountsReceivableTimelineEntryResponse MapTimelineEntry(AccountsReceivableTimelineEntry item)
    {
        return new AccountsReceivableTimelineEntryResponse
        {
            AtUtc = item.AtUtc,
            Kind = item.Kind,
            Title = item.Title,
            Description = item.Description,
            SourceType = item.SourceType,
            SourceId = item.SourceId,
            Status = item.Status
        };
    }

    private static AccountsReceivablePaymentSummaryItemResponse MapPaymentProjection(AccountsReceivablePaymentOperationalProjection item)
    {
        return new AccountsReceivablePaymentSummaryItemResponse
        {
            PaymentId = item.PaymentId,
            ReceivedAtUtc = item.ReceivedAtUtc,
            Amount = item.Amount,
            AppliedAmount = item.AppliedAmount,
            UnappliedAmount = item.UnappliedAmount,
            CurrencyCode = item.CurrencyCode,
            Reference = item.Reference,
            PayerName = item.PayerName,
            FiscalReceiverId = item.FiscalReceiverId,
            OperationalStatus = item.OperationalStatus.ToString(),
            RepStatus = item.RepStatus.ToString(),
            RepDocumentStatus = item.RepDocumentStatus,
            ApplicationsCount = item.ApplicationsCount,
            LinkedFiscalDocumentId = item.LinkedFiscalDocumentId,
            RepReservedAmount = item.RepReservedAmount,
            RepFiscalizedAmount = item.RepFiscalizedAmount
        };
    }

    private static AccountsReceivableReceiverWorkspaceResponse MapReceiverWorkspace(AccountsReceivableReceiverWorkspaceProjection workspace)
    {
        return new AccountsReceivableReceiverWorkspaceResponse
        {
            FiscalReceiverId = workspace.FiscalReceiverId,
            Rfc = workspace.Rfc,
            LegalName = workspace.LegalName,
            Summary = new AccountsReceivableReceiverWorkspaceSummaryResponse
            {
                PendingBalanceTotal = workspace.Summary.PendingBalanceTotal,
                OverdueBalanceTotal = workspace.Summary.OverdueBalanceTotal,
                CurrentBalanceTotal = workspace.Summary.CurrentBalanceTotal,
                OpenInvoicesCount = workspace.Summary.OpenInvoicesCount,
                OverdueInvoicesCount = workspace.Summary.OverdueInvoicesCount,
                PaymentsCount = workspace.Summary.PaymentsCount,
                PaymentsWithUnappliedAmountCount = workspace.Summary.PaymentsWithUnappliedAmountCount,
                PaymentsPendingRepCount = workspace.Summary.PaymentsPendingRepCount,
                NextFollowUpAtUtc = workspace.Summary.NextFollowUpAtUtc,
                HasPendingCommitment = workspace.Summary.HasPendingCommitment,
                PendingCommitmentsCount = workspace.Summary.PendingCommitmentsCount,
                RecentNotesCount = workspace.Summary.RecentNotesCount,
                PaymentsReadyToPrepareRepCount = workspace.Summary.PaymentsReadyToPrepareRepCount,
                PaymentsPreparedRepCount = workspace.Summary.PaymentsPreparedRepCount,
                PaymentsStampedRepCount = workspace.Summary.PaymentsStampedRepCount
            },
            Invoices = workspace.Invoices.Select(MapPortfolioItem).ToList(),
            Payments = workspace.Payments.Select(MapPaymentProjection).ToList(),
            PendingCommitments = workspace.PendingCommitments.Select(item => new AccountsReceivableReceiverWorkspaceCommitmentResponse
            {
                Id = item.Id,
                AccountsReceivableInvoiceId = item.AccountsReceivableInvoiceId,
                PromisedAmount = item.PromisedAmount,
                PromisedDateUtc = item.PromisedDateUtc,
                Status = item.Status,
                Notes = item.Notes,
                CreatedAtUtc = item.CreatedAtUtc
            }).ToList(),
            RecentNotes = workspace.RecentNotes.Select(item => new AccountsReceivableReceiverWorkspaceNoteResponse
            {
                Id = item.Id,
                AccountsReceivableInvoiceId = item.AccountsReceivableInvoiceId,
                NoteType = item.NoteType,
                Content = item.Content,
                NextFollowUpAtUtc = item.NextFollowUpAtUtc,
                CreatedAtUtc = item.CreatedAtUtc,
                CreatedByUsername = item.CreatedByUsername
            }).ToList()
        };
    }
}

public class CreateAccountsReceivableInvoiceRequest
{
    public int? OverrideCreditDays { get; set; }
}

public class CreateAccountsReceivableInvoiceResponse
{
    public string Outcome { get; set; } = string.Empty;

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public long FiscalDocumentId { get; set; }

    public AccountsReceivableInvoiceResponse? AccountsReceivableInvoice { get; set; }
}

public class AccountsReceivableInvoiceResponse
{
    public long Id { get; set; }

    public long? BillingDocumentId { get; set; }

    public long? FiscalDocumentId { get; set; }

    public long? FiscalStampId { get; set; }

    public long? FiscalReceiverId { get; set; }

    public string? ReceiverRfc { get; set; }

    public string? ReceiverLegalName { get; set; }

    public string? FiscalSeries { get; set; }

    public string? FiscalFolio { get; set; }

    public string? FiscalUuid { get; set; }

    public string Status { get; set; } = string.Empty;

    public string PaymentMethodSat { get; set; } = string.Empty;

    public string PaymentFormSatInitial { get; set; } = string.Empty;

    public bool IsCreditSale { get; set; }

    public int? CreditDays { get; set; }

    public DateTime IssuedAtUtc { get; set; }

    public DateTime? DueAtUtc { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public decimal Total { get; set; }

    public decimal PaidTotal { get; set; }

    public decimal OutstandingBalance { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public string AgingBucket { get; set; } = string.Empty;

    public bool HasPendingCommitment { get; set; }

    public DateTime? NextCommitmentDateUtc { get; set; }

    public DateTime? NextFollowUpAtUtc { get; set; }

    public bool FollowUpPending { get; set; }

    public List<CollectionCommitmentResponse> CollectionCommitments { get; set; } = [];

    public List<CollectionNoteResponse> CollectionNotes { get; set; } = [];

    public List<AccountsReceivablePaymentResponse> RelatedPayments { get; set; } = [];

    public List<AccountsReceivablePaymentComplementSummaryResponse> RelatedPaymentComplements { get; set; } = [];

    public List<AccountsReceivableTimelineEntryResponse> Timeline { get; set; } = [];

    public List<AccountsReceivablePaymentApplicationResponse> Applications { get; set; } = [];
}

public class AccountsReceivablePortfolioResponse
{
    public List<AccountsReceivablePortfolioItemResponse> Items { get; set; } = [];
}

public class AccountsReceivableReceiverWorkspaceResponse
{
    public long FiscalReceiverId { get; set; }

    public string Rfc { get; set; } = string.Empty;

    public string LegalName { get; set; } = string.Empty;

    public AccountsReceivableReceiverWorkspaceSummaryResponse Summary { get; set; } = new();

    public List<AccountsReceivablePortfolioItemResponse> Invoices { get; set; } = [];

    public List<AccountsReceivablePaymentSummaryItemResponse> Payments { get; set; } = [];

    public List<AccountsReceivableReceiverWorkspaceCommitmentResponse> PendingCommitments { get; set; } = [];

    public List<AccountsReceivableReceiverWorkspaceNoteResponse> RecentNotes { get; set; } = [];
}

public class AccountsReceivableReceiverWorkspaceSummaryResponse
{
    public decimal PendingBalanceTotal { get; set; }

    public decimal OverdueBalanceTotal { get; set; }

    public decimal CurrentBalanceTotal { get; set; }

    public int OpenInvoicesCount { get; set; }

    public int OverdueInvoicesCount { get; set; }

    public int PaymentsCount { get; set; }

    public int PaymentsWithUnappliedAmountCount { get; set; }

    public int PaymentsPendingRepCount { get; set; }

    public DateTime? NextFollowUpAtUtc { get; set; }

    public bool HasPendingCommitment { get; set; }

    public int PendingCommitmentsCount { get; set; }

    public int RecentNotesCount { get; set; }

    public int PaymentsReadyToPrepareRepCount { get; set; }

    public int PaymentsPreparedRepCount { get; set; }

    public int PaymentsStampedRepCount { get; set; }
}

public class AccountsReceivableReceiverWorkspaceCommitmentResponse
{
    public long Id { get; set; }

    public long AccountsReceivableInvoiceId { get; set; }

    public decimal PromisedAmount { get; set; }

    public DateTime PromisedDateUtc { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}

public class AccountsReceivableReceiverWorkspaceNoteResponse
{
    public long Id { get; set; }

    public long AccountsReceivableInvoiceId { get; set; }

    public string NoteType { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public DateTime? NextFollowUpAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public string? CreatedByUsername { get; set; }
}

public class AccountsReceivablePortfolioItemResponse
{
    public long AccountsReceivableInvoiceId { get; set; }

    public long? FiscalDocumentId { get; set; }

    public long? FiscalReceiverId { get; set; }

    public string? ReceiverRfc { get; set; }

    public string? ReceiverLegalName { get; set; }

    public string? FiscalSeries { get; set; }

    public string? FiscalFolio { get; set; }

    public string? FiscalUuid { get; set; }

    public decimal Total { get; set; }

    public decimal PaidTotal { get; set; }

    public decimal OutstandingBalance { get; set; }

    public DateTime IssuedAtUtc { get; set; }

    public DateTime? DueAtUtc { get; set; }

    public string Status { get; set; } = string.Empty;

    public int DaysPastDue { get; set; }

    public string AgingBucket { get; set; } = string.Empty;

    public bool HasPendingCommitment { get; set; }

    public DateTime? NextCommitmentDateUtc { get; set; }

    public DateTime? NextFollowUpAtUtc { get; set; }

    public bool FollowUpPending { get; set; }
}

public class CreateCollectionCommitmentRequest
{
    public decimal PromisedAmount { get; set; }

    public DateTime PromisedDateUtc { get; set; }

    public string? Notes { get; set; }
}

public class CreateCollectionCommitmentResponse
{
    public string Outcome { get; set; } = string.Empty;

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public CollectionCommitmentResponse? Commitment { get; set; }
}

public class CollectionCommitmentsResponse
{
    public List<CollectionCommitmentResponse> Items { get; set; } = [];
}

public class CollectionCommitmentResponse
{
    public long Id { get; set; }

    public long AccountsReceivableInvoiceId { get; set; }

    public decimal PromisedAmount { get; set; }

    public DateTime PromisedDateUtc { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public string? CreatedByUsername { get; set; }
}

public class CreateCollectionNoteRequest
{
    public string NoteType { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public DateTime? NextFollowUpAtUtc { get; set; }
}

public class CreateCollectionNoteResponse
{
    public string Outcome { get; set; } = string.Empty;

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public CollectionNoteResponse? Note { get; set; }
}

public class CollectionNotesResponse
{
    public List<CollectionNoteResponse> Items { get; set; } = [];
}

public class CollectionNoteResponse
{
    public long Id { get; set; }

    public long AccountsReceivableInvoiceId { get; set; }

    public string NoteType { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public DateTime? NextFollowUpAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public string? CreatedByUsername { get; set; }
}

public class AccountsReceivablePaymentComplementSummaryResponse
{
    public long PaymentComplementId { get; set; }

    public long AccountsReceivablePaymentId { get; set; }

    public string Status { get; set; } = string.Empty;

    public decimal TotalPaymentsAmount { get; set; }

    public DateTime IssuedAtUtc { get; set; }

    public DateTime PaymentDateUtc { get; set; }

    public string? Uuid { get; set; }

    public DateTime? StampedAtUtc { get; set; }

    public DateTime? CancelledAtUtc { get; set; }
}

public class AccountsReceivableTimelineEntryResponse
{
    public DateTime AtUtc { get; set; }

    public string Kind { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string SourceType { get; set; } = string.Empty;

    public long SourceId { get; set; }

    public string? Status { get; set; }
}

public class CreateAccountsReceivablePaymentRequest
{
    public DateTime PaymentDateUtc { get; set; }

    public string PaymentFormSat { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string? Reference { get; set; }

    public string? Notes { get; set; }

    public long? ReceivedFromFiscalReceiverId { get; set; }
}

public class CreateAccountsReceivablePaymentResponse
{
    public string Outcome { get; set; } = string.Empty;

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public AccountsReceivablePaymentResponse? Payment { get; set; }
}

public class AccountsReceivablePaymentResponse
{
    public long Id { get; set; }

    public DateTime PaymentDateUtc { get; set; }

    public string PaymentFormSat { get; set; } = string.Empty;

    public string CurrencyCode { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public decimal AppliedTotal { get; set; }

    public decimal RemainingAmount { get; set; }

    public string? Reference { get; set; }

    public string? Notes { get; set; }

    public long? ReceivedFromFiscalReceiverId { get; set; }

    public string OperationalStatus { get; set; } = string.Empty;

    public string RepStatus { get; set; } = string.Empty;

    public string? RepDocumentStatus { get; set; }

    public decimal RepReservedAmount { get; set; }

    public decimal RepFiscalizedAmount { get; set; }

    public int ApplicationsCount { get; set; }

    public long? LinkedFiscalDocumentId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public List<AccountsReceivablePaymentApplicationResponse> Applications { get; set; } = [];
}

public class ApplyAccountsReceivablePaymentRequest
{
    public List<ApplyAccountsReceivablePaymentRowRequest> Applications { get; set; } = [];
}

public class ApplyAccountsReceivablePaymentRowRequest
{
    public long AccountsReceivableInvoiceId { get; set; }

    public decimal AppliedAmount { get; set; }
}

public class ApplyAccountsReceivablePaymentResponse
{
    public string Outcome { get; set; } = string.Empty;

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public long AccountsReceivablePaymentId { get; set; }

    public int AppliedCount { get; set; }

    public decimal RemainingPaymentAmount { get; set; }

    public AccountsReceivablePaymentResponse? Payment { get; set; }

    public List<AccountsReceivablePaymentApplicationResponse> Applications { get; set; } = [];
}

public class AccountsReceivablePaymentsResponse
{
    public List<AccountsReceivablePaymentSummaryItemResponse> Items { get; set; } = [];
}

public class AccountsReceivablePaymentSummaryItemResponse
{
    public long PaymentId { get; set; }

    public DateTime ReceivedAtUtc { get; set; }

    public decimal Amount { get; set; }

    public decimal AppliedAmount { get; set; }

    public decimal UnappliedAmount { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public string? Reference { get; set; }

    public string? PayerName { get; set; }

    public long? FiscalReceiverId { get; set; }

    public string OperationalStatus { get; set; } = string.Empty;

    public string RepStatus { get; set; } = string.Empty;

    public string? RepDocumentStatus { get; set; }

    public int ApplicationsCount { get; set; }

    public long? LinkedFiscalDocumentId { get; set; }

    public decimal RepReservedAmount { get; set; }

    public decimal RepFiscalizedAmount { get; set; }
}

public class PreparePaymentComplementRequest
{
    public DateTime? IssuedAtUtc { get; set; }
}

public class PreparePaymentComplementResponse
{
    public string Outcome { get; set; } = string.Empty;

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public long AccountsReceivablePaymentId { get; set; }

    public long? PaymentComplementId { get; set; }

    public string? Status { get; set; }
}

public class AccountsReceivablePaymentApplicationResponse
{
    public long Id { get; set; }

    public long AccountsReceivablePaymentId { get; set; }

    public long AccountsReceivableInvoiceId { get; set; }

    public int ApplicationSequence { get; set; }

    public decimal AppliedAmount { get; set; }

    public decimal PreviousBalance { get; set; }

    public decimal NewBalance { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
