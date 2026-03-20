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

        var group = endpoints.MapGroup("/api/accounts-receivable")
            .WithTags("AccountsReceivable")
            .RequireAuthorization(AuthorizationPolicyNames.Authenticated);

        group.MapPost("/payments", CreateAccountsReceivablePaymentAsync)
            .RequireAuthorization(AuthorizationPolicyNames.OperatorOrAbove)
            .WithName("CreateAccountsReceivablePayment")
            .WithSummary("Create an accounts receivable payment event")
            .Produces<CreateAccountsReceivablePaymentResponse>(StatusCodes.Status200OK)
            .Produces<CreateAccountsReceivablePaymentResponse>(StatusCodes.Status400BadRequest);

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
        GetAccountsReceivableInvoiceByFiscalDocumentIdService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(fiscalDocumentId, cancellationToken);
        if (result.Outcome == GetAccountsReceivableInvoiceByFiscalDocumentIdOutcome.NotFound || result.AccountsReceivableInvoice is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(MapInvoice(result.AccountsReceivableInvoice));
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
                Notes = request.Notes
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

        return TypedResults.Ok(MapPayment(result.AccountsReceivablePayment));
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

    private static AccountsReceivableInvoiceResponse MapInvoice(AccountsReceivableInvoice invoice)
    {
        return new AccountsReceivableInvoiceResponse
        {
            Id = invoice.Id,
            BillingDocumentId = invoice.BillingDocumentId,
            FiscalDocumentId = invoice.FiscalDocumentId,
            FiscalStampId = invoice.FiscalStampId,
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
            Applications = invoice.Applications
                .OrderBy(x => x.ApplicationSequence)
                .Select(MapApplication)
                .ToList()
        };
    }

    private static AccountsReceivablePaymentResponse MapPayment(AccountsReceivablePayment payment)
    {
        var applications = payment.Applications
            .OrderBy(x => x.ApplicationSequence)
            .Select(MapApplication)
            .ToList();

        return new AccountsReceivablePaymentResponse
        {
            Id = payment.Id,
            PaymentDateUtc = payment.PaymentDateUtc,
            PaymentFormSat = payment.PaymentFormSat,
            CurrencyCode = payment.CurrencyCode,
            Amount = payment.Amount,
            AppliedTotal = payment.Applications.Sum(x => x.AppliedAmount),
            RemainingAmount = payment.Amount - payment.Applications.Sum(x => x.AppliedAmount),
            Reference = payment.Reference,
            Notes = payment.Notes,
            ReceivedFromFiscalReceiverId = payment.ReceivedFromFiscalReceiverId,
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

    public long BillingDocumentId { get; set; }

    public long FiscalDocumentId { get; set; }

    public long FiscalStampId { get; set; }

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

    public List<AccountsReceivablePaymentApplicationResponse> Applications { get; set; } = [];
}

public class CreateAccountsReceivablePaymentRequest
{
    public DateTime PaymentDateUtc { get; set; }

    public string PaymentFormSat { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string? Reference { get; set; }

    public string? Notes { get; set; }
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
