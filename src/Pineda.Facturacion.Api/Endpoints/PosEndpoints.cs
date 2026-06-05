using Microsoft.AspNetCore.Http.HttpResults;
using Pineda.Facturacion.Api.Security;
using Pineda.Facturacion.Application.Security;
using Pineda.Facturacion.Application.UseCases.Pos;

namespace Pineda.Facturacion.Api.Endpoints;

public static class PosEndpoints
{
    public static IEndpointRouteBuilder MapPosEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/pos")
            .WithTags("POS")
            .RequireAuthorization(AuthorizationPolicyNames.PosCreditRead)
            .RequireCors(CorsPolicyNames.PosClient);

        group.MapGet("/receivers/search", SearchReceiversAsync)
            .WithName("SearchPosReceivers")
            .WithSummary("Search fiscal receivers for POS credit validation")
            .Produces<IReadOnlyList<PosReceiverSearchResponse>>(StatusCodes.Status200OK)
            .Produces<PosValidationErrorResponse>(StatusCodes.Status400BadRequest);

        group.MapGet("/receivers/{fiscalReceiverId:long}/credit-status", GetReceiverCreditStatusAsync)
            .WithName("GetPosReceiverCreditStatus")
            .WithSummary("Get a lightweight credit status snapshot for a fiscal receiver")
            .Produces<PosReceiverCreditStatusResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/receivers/{fiscalReceiverId:long}/credit-check", CheckReceiverCreditAsync)
            .WithName("CheckPosReceiverCredit")
            .WithSummary("Validate whether a sale can be approved against the receiver credit line")
            .Produces<PosReceiverCreditCheckResponse>(StatusCodes.Status200OK)
            .Produces<PosValidationErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<Results<Ok<IReadOnlyList<PosReceiverSearchResponse>>, BadRequest<PosValidationErrorResponse>>> SearchReceiversAsync(
        string? term,
        SearchPosReceiversService service,
        CancellationToken cancellationToken)
    {
        var normalizedTerm = term?.Trim() ?? string.Empty;
        if (normalizedTerm.Length < 3)
        {
            return ValidationError(
                PosCreditValidationErrorCodes.TermTooShort,
                "Search term must contain at least 3 characters.");
        }

        var result = await service.ExecuteAsync(normalizedTerm, cancellationToken);
        IReadOnlyList<PosReceiverSearchResponse> items = result.Items.Select(MapSearchItem).ToList();
        return TypedResults.Ok(items);
    }

    private static async Task<Results<Ok<PosReceiverCreditStatusResponse>, NotFound>> GetReceiverCreditStatusAsync(
        long fiscalReceiverId,
        GetPosReceiverCreditStatusService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(fiscalReceiverId, cancellationToken);
        if (result.Outcome == GetPosReceiverCreditStatusOutcome.NotFound || result.CreditStatus is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(MapCreditStatus(result.CreditStatus));
    }

    private static async Task<Results<Ok<PosReceiverCreditCheckResponse>, BadRequest<PosValidationErrorResponse>, NotFound>> CheckReceiverCreditAsync(
        long fiscalReceiverId,
        PosReceiverCreditCheckRequest request,
        CheckPosReceiverCreditService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(
            new CheckPosReceiverCreditCommand
            {
                FiscalReceiverId = fiscalReceiverId,
                SaleAmount = request.SaleAmount,
                CurrencyCode = request.CurrencyCode
            },
            cancellationToken);

        return result.Outcome switch
        {
            CheckPosReceiverCreditOutcome.Approved or CheckPosReceiverCreditOutcome.Blocked
                when result.Evaluation is not null => TypedResults.Ok(MapCreditCheck(result.Evaluation)),
            CheckPosReceiverCreditOutcome.NotFound => TypedResults.NotFound(),
            _ => ValidationError(
                result.ErrorCode ?? PosCreditValidationErrorCodes.InvalidSaleAmount,
                result.ErrorMessage ?? "The request is invalid.")
        };
    }

    private static PosReceiverSearchResponse MapSearchItem(PosReceiverSearchItem item)
    {
        return new PosReceiverSearchResponse
        {
            FiscalReceiverId = item.FiscalReceiverId,
            Rfc = item.Rfc,
            LegalName = item.LegalName
        };
    }

    private static PosReceiverCreditStatusResponse MapCreditStatus(PosReceiverCreditStatus status)
    {
        return new PosReceiverCreditStatusResponse
        {
            FiscalReceiverId = status.FiscalReceiverId,
            Rfc = status.Rfc,
            LegalName = status.LegalName,
            CreditEnabled = status.CreditEnabled,
            ApprovedCreditLimitAmount = status.ApprovedCreditLimitAmount,
            PendingBalanceTotal = status.PendingBalanceTotal,
            OverdueBalanceTotal = status.OverdueBalanceTotal,
            CurrentBalanceTotal = status.CurrentBalanceTotal,
            AvailableCreditAmount = status.AvailableCreditAmount,
            OpenInvoicesCount = status.OpenInvoicesCount,
            OverdueInvoicesCount = status.OverdueInvoicesCount,
            CanSellOnCredit = status.CanSellOnCredit,
            BlockReason = status.BlockReason
        };
    }

    private static PosReceiverCreditCheckResponse MapCreditCheck(PosReceiverCreditCheckEvaluation evaluation)
    {
        return new PosReceiverCreditCheckResponse
        {
            Approved = evaluation.Approved,
            AvailableCreditAmount = evaluation.AvailableCreditAmount,
            SaleAmount = evaluation.SaleAmount,
            RemainingCreditAmount = evaluation.RemainingCreditAmount,
            BlockReason = evaluation.BlockReason
        };
    }

    private static BadRequest<PosValidationErrorResponse> ValidationError(string errorCode, string errorMessage)
    {
        return TypedResults.BadRequest(new PosValidationErrorResponse
        {
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        });
    }

    public sealed class PosReceiverCreditCheckRequest
    {
        public decimal SaleAmount { get; init; }

        public string CurrencyCode { get; init; } = string.Empty;
    }

    public sealed class PosValidationErrorResponse
    {
        public string ErrorCode { get; init; } = string.Empty;

        public string ErrorMessage { get; init; } = string.Empty;
    }

    public sealed class PosReceiverSearchResponse
    {
        public long FiscalReceiverId { get; init; }

        public string Rfc { get; init; } = string.Empty;

        public string LegalName { get; init; } = string.Empty;
    }

    public sealed class PosReceiverCreditStatusResponse
    {
        public long FiscalReceiverId { get; init; }

        public string Rfc { get; init; } = string.Empty;

        public string LegalName { get; init; } = string.Empty;

        public bool CreditEnabled { get; init; }

        public decimal ApprovedCreditLimitAmount { get; init; }

        public decimal PendingBalanceTotal { get; init; }

        public decimal OverdueBalanceTotal { get; init; }

        public decimal CurrentBalanceTotal { get; init; }

        public decimal AvailableCreditAmount { get; init; }

        public int OpenInvoicesCount { get; init; }

        public int OverdueInvoicesCount { get; init; }

        public bool CanSellOnCredit { get; init; }

        public string? BlockReason { get; init; }
    }

    public sealed class PosReceiverCreditCheckResponse
    {
        public bool Approved { get; init; }

        public decimal AvailableCreditAmount { get; init; }

        public decimal SaleAmount { get; init; }

        public decimal RemainingCreditAmount { get; init; }

        public string? BlockReason { get; init; }
    }
}
