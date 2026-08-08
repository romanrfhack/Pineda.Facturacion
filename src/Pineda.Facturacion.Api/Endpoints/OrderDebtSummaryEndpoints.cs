using Microsoft.AspNetCore.Http.HttpResults;
using Pineda.Facturacion.Api.Security;
using Pineda.Facturacion.Application.Security;
using Pineda.Facturacion.Application.UseCases.Orders;

namespace Pineda.Facturacion.Api.Endpoints;

public static class OrderDebtSummaryEndpoints
{
    public static IEndpointRouteBuilder MapOrderDebtSummaryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/orders/debt-summary")
            .WithTags("Orders")
            .RequireAuthorization(AuthorizationPolicyNames.OperatorOrAbove);

        group.MapPost("/eligibility", EvaluateOrderDebtSummaryEligibilityAsync)
            .WithName("EvaluateOrderDebtSummaryEligibility")
            .WithSummary("Evaluate whether selected legacy orders can be included in a debt summary")
            .Produces<OrderDebtSummaryEligibilityResponse>(StatusCodes.Status200OK)
            .Produces<OrderDebtSummaryEligibilityResponse>(StatusCodes.Status400BadRequest);

        group.MapPost("/preview", PreviewOrderDebtSummaryAsync)
            .WithName("PreviewOrderDebtSummary")
            .WithSummary("Generate an HTML/PDF preview for an orders debt summary")
            .Produces<OrderDebtSummaryPreviewResponse>(StatusCodes.Status200OK)
            .Produces<OrderDebtSummaryPreviewResponse>(StatusCodes.Status400BadRequest)
            .Produces<OrderDebtSummaryPreviewResponse>(StatusCodes.Status404NotFound);

        group.MapPost("/send", SendOrderDebtSummaryAsync)
            .WithName("SendOrderDebtSummary")
            .WithSummary("Send an email summary for selected legacy orders")
            .Produces<SendOrderDebtSummaryResponse>(StatusCodes.Status200OK)
            .Produces<SendOrderDebtSummaryResponse>(StatusCodes.Status400BadRequest)
            .Produces<SendOrderDebtSummaryResponse>(StatusCodes.Status404NotFound)
            .Produces<SendOrderDebtSummaryResponse>(StatusCodes.Status503ServiceUnavailable);

        return endpoints;
    }

    private static async Task<Results<Ok<OrderDebtSummaryEligibilityResponse>, BadRequest<OrderDebtSummaryEligibilityResponse>>> EvaluateOrderDebtSummaryEligibilityAsync(
        OrderDebtSummaryEligibilityRequest request,
        OrderDebtSummaryEligibilityService service,
        CancellationToken cancellationToken)
    {
        var resolution = await service.EvaluateAsync(request.LegacyOrderIds ?? [], cancellationToken);
        var response = new OrderDebtSummaryEligibilityResponse
        {
            Success = resolution.IsSuccess,
            ErrorMessage = resolution.ErrorMessage,
            RequestedOrderCount = resolution.RequestedOrderIds.Count,
            EligibleOrderCount = resolution.Items.Count(item => item.Decision.CanInclude),
            BlockedOrderCount = resolution.Items.Count(item => !item.Decision.CanInclude),
            MissingOrderIds = resolution.MissingOrderIds,
            Items = resolution.Items.Select(MapEligibilityItem).ToArray()
        };

        return resolution.IsSuccess
            ? TypedResults.Ok(response)
            : TypedResults.BadRequest(response);
    }

    private static async Task<Results<Ok<OrderDebtSummaryPreviewResponse>, BadRequest<OrderDebtSummaryPreviewResponse>, NotFound<OrderDebtSummaryPreviewResponse>>> PreviewOrderDebtSummaryAsync(
        OrderDebtSummaryRequest request,
        PreviewOrderDebtSummaryService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(MapCommand(request), cancellationToken);
        var response = MapPreview(result);

        return result.Outcome switch
        {
            OrderDebtSummaryOutcome.Found => TypedResults.Ok(response),
            OrderDebtSummaryOutcome.NotFound => TypedResults.NotFound(response),
            _ => TypedResults.BadRequest(response)
        };
    }

    private static async Task<Results<Ok<SendOrderDebtSummaryResponse>, BadRequest<SendOrderDebtSummaryResponse>, NotFound<SendOrderDebtSummaryResponse>, JsonHttpResult<SendOrderDebtSummaryResponse>>> SendOrderDebtSummaryAsync(
        OrderDebtSummaryRequest request,
        SendOrderDebtSummaryService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(MapCommand(request), cancellationToken);
        var response = MapSend(result);

        return result.Outcome switch
        {
            OrderDebtSummaryOutcome.Sent => TypedResults.Ok(response),
            OrderDebtSummaryOutcome.NotFound => TypedResults.NotFound(response),
            OrderDebtSummaryOutcome.DeliveryFailed => TypedResults.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable),
            OrderDebtSummaryOutcome.HistoryFailed => TypedResults.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable),
            _ => TypedResults.BadRequest(response)
        };
    }

    private static OrderDebtSummaryCommand MapCommand(OrderDebtSummaryRequest request)
    {
        var options = request.Options ?? new OrderDebtSummaryIncludeOptionsRequest();
        return new OrderDebtSummaryCommand
        {
            ReceiverId = request.ReceiverId,
            LegacyOrderIds = request.LegacyOrderIds ?? [],
            To = request.To ?? [],
            Cc = request.Cc ?? [],
            Bcc = request.Bcc ?? [],
            Subject = request.Subject,
            Message = request.Message,
            Format = string.IsNullOrWhiteSpace(request.Format) ? "html" : request.Format!,
            Options = new OrderDebtSummaryIncludeOptions
            {
                IncludeOrderTable = options.IncludeOrderTable,
                IncludeTotals = options.IncludeTotals,
                IncludeReceiverFiscalData = options.IncludeReceiverFiscalData,
                IncludeIssuerData = options.IncludeIssuerData,
                IncludePaymentInstructions = options.IncludePaymentInstructions,
                IncludeBillingStatus = options.IncludeBillingStatus
            }
        };
    }

    private static OrderDebtSummaryEligibilityItemResponse MapEligibilityItem(
        OrderDebtSummaryEligibilityItem item)
    {
        var trace = item.Trace;
        return new OrderDebtSummaryEligibilityItemResponse
        {
            LegacyOrderId = item.LegacyOrder.LegacyOrderId,
            CanInclude = item.Decision.CanInclude,
            RequiresReview = item.Decision.RequiresReview,
            Classification = item.Decision.Classification.ToString(),
            ReasonCode = item.Decision.ReasonCode,
            Message = item.Decision.Message,
            DisplayStatus = item.Decision.DisplayStatus,
            ReportGroupKey = item.Decision.ReportGroupKey,
            CurrencyCode = item.Decision.CurrencyCode,
            AmountDue = item.Decision.AmountDue,
            AmountDueContribution = item.AmountDueContribution,
            BillingDocumentId = trace?.BillingDocumentId,
            FiscalDocumentId = trace?.FiscalDocumentId,
            FiscalUuid = trace?.FiscalUuid,
            AccountsReceivableInvoiceId = trace?.AccountsReceivableInvoiceId,
            AccountsReceivableStatus = trace?.AccountsReceivableStatus?.ToString(),
            InvoiceTotal = trace?.InvoiceTotal,
            PaidTotal = trace?.PaidTotal,
            OutstandingBalance = trace?.OutstandingBalance,
            RelatedLegacyOrderIds = trace?.RelatedLegacyOrderIds ?? [item.LegacyOrder.LegacyOrderId]
        };
    }

    private static OrderDebtSummaryPreviewResponse MapPreview(OrderDebtSummaryPreviewResult result)
    {
        return new OrderDebtSummaryPreviewResponse
        {
            Outcome = result.Outcome.ToString(),
            Success = result.IsSuccess,
            ErrorMessage = result.ErrorMessage,
            Html = result.Html,
            PdfBase64 = result.PdfContent is null ? null : Convert.ToBase64String(result.PdfContent),
            PdfFileName = result.PdfFileName,
            PdfErrorMessage = result.PdfErrorMessage,
            Summary = result.Document is null ? null : MapSelection(result.Document.Selection),
            FinalSummary = result.Document is null ? null : MapFinal(result.Document),
            ValidationIssues = result.ValidationIssues.Select(MapValidationIssue).ToArray()
        };
    }

    private static SendOrderDebtSummaryResponse MapSend(SendOrderDebtSummaryResult result)
    {
        return new SendOrderDebtSummaryResponse
        {
            Outcome = result.Outcome.ToString(),
            Success = result.IsSuccess,
            ErrorMessage = result.ErrorMessage,
            SentAt = result.SentAtUtc,
            HistoryId = result.HistoryId,
            EmailProviderMessageId = result.EmailProviderMessageId,
            Summary = result.Document is null ? null : MapSelection(result.Document.Selection),
            ValidationIssues = result.ValidationIssues.Select(MapValidationIssue).ToArray()
        };
    }

    private static OrderDebtSummaryValidationIssueResponse MapValidationIssue(
        OrderDebtSummaryValidationIssue issue)
    {
        return new OrderDebtSummaryValidationIssueResponse
        {
            LegacyOrderId = issue.LegacyOrderId,
            Classification = issue.Classification,
            ReasonCode = issue.ReasonCode,
            Message = issue.Message,
            RequiresReview = issue.RequiresReview,
            BillingDocumentId = issue.BillingDocumentId,
            FiscalDocumentId = issue.FiscalDocumentId,
            FiscalUuid = issue.FiscalUuid,
            AccountsReceivableInvoiceId = issue.AccountsReceivableInvoiceId,
            AccountsReceivableStatus = issue.AccountsReceivableStatus,
            CurrencyCode = issue.CurrencyCode,
            InvoiceTotal = issue.InvoiceTotal,
            PaidTotal = issue.PaidTotal,
            OutstandingBalance = issue.OutstandingBalance,
            RelatedLegacyOrderIds = issue.RelatedLegacyOrderIds
        };
    }

    private static OrderDebtSummarySelectionResponse MapSelection(OrderDebtSummarySelection selection)
    {
        return new OrderDebtSummarySelectionResponse
        {
            OrderCount = selection.OrderCount,
            Total = selection.Total,
            TotalsByCurrency = selection.TotalsByCurrency.Select(total => new OrderDebtSummaryTotalByCurrencyResponse
            {
                CurrencyCode = total.CurrencyCode,
                OrderCount = total.OrderCount,
                Total = total.Total
            }).ToList()
        };
    }

    private static OrderDebtSummaryFinalResponse MapFinal(OrderDebtSummaryDocument document)
    {
        return new OrderDebtSummaryFinalResponse
        {
            To = document.To.ToList(),
            Cc = document.Cc.ToList(),
            Bcc = document.Bcc.ToList(),
            Subject = document.Subject,
            OrderCount = document.Selection.OrderCount,
            Format = document.Format.ToString(),
            TotalsByCurrency = document.Selection.TotalsByCurrency.Select(total => new OrderDebtSummaryTotalByCurrencyResponse
            {
                CurrencyCode = total.CurrencyCode,
                OrderCount = total.OrderCount,
                Total = total.Total
            }).ToList()
        };
    }
}

public sealed class OrderDebtSummaryEligibilityRequest
{
    public List<string> LegacyOrderIds { get; init; } = [];
}

public sealed class OrderDebtSummaryEligibilityResponse
{
    public bool Success { get; init; }

    public string? ErrorMessage { get; init; }

    public int RequestedOrderCount { get; init; }

    public int EligibleOrderCount { get; init; }

    public int BlockedOrderCount { get; init; }

    public IReadOnlyList<string> MissingOrderIds { get; init; } = [];

    public IReadOnlyList<OrderDebtSummaryEligibilityItemResponse> Items { get; init; } = [];
}

public sealed class OrderDebtSummaryEligibilityItemResponse
{
    public string LegacyOrderId { get; init; } = string.Empty;

    public bool CanInclude { get; init; }

    public bool RequiresReview { get; init; }

    public string Classification { get; init; } = string.Empty;

    public string ReasonCode { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string DisplayStatus { get; init; } = string.Empty;

    public string ReportGroupKey { get; init; } = string.Empty;

    public string CurrencyCode { get; init; } = "MXN";

    public decimal AmountDue { get; init; }

    public decimal AmountDueContribution { get; init; }

    public long? BillingDocumentId { get; init; }

    public long? FiscalDocumentId { get; init; }

    public string? FiscalUuid { get; init; }

    public long? AccountsReceivableInvoiceId { get; init; }

    public string? AccountsReceivableStatus { get; init; }

    public decimal? InvoiceTotal { get; init; }

    public decimal? PaidTotal { get; init; }

    public decimal? OutstandingBalance { get; init; }

    public IReadOnlyList<string> RelatedLegacyOrderIds { get; init; } = [];
}

public sealed class OrderDebtSummaryRequest
{
    public List<string> LegacyOrderIds { get; init; } = [];

    public long ReceiverId { get; init; }

    public List<string> To { get; init; } = [];

    public List<string> Cc { get; init; } = [];

    public List<string> Bcc { get; init; } = [];

    public string? Subject { get; init; }

    public string? Message { get; init; }

    public string? Format { get; init; } = "html";

    public OrderDebtSummaryIncludeOptionsRequest? Options { get; init; }
}

public sealed class OrderDebtSummaryIncludeOptionsRequest
{
    public bool IncludeOrderTable { get; init; } = true;

    public bool IncludeTotals { get; init; } = true;

    public bool IncludeReceiverFiscalData { get; init; } = true;

    public bool IncludeIssuerData { get; init; } = true;

    public bool IncludePaymentInstructions { get; init; } = true;

    public bool IncludeBillingStatus { get; init; } = true;
}

public sealed class OrderDebtSummaryPreviewResponse
{
    public string Outcome { get; init; } = string.Empty;

    public bool Success { get; init; }

    public string? ErrorMessage { get; init; }

    public string? Html { get; init; }

    public string? PdfBase64 { get; init; }

    public string? PdfFileName { get; init; }

    public string? PdfErrorMessage { get; init; }

    public OrderDebtSummarySelectionResponse? Summary { get; init; }

    public OrderDebtSummaryFinalResponse? FinalSummary { get; init; }

    public IReadOnlyList<OrderDebtSummaryValidationIssueResponse> ValidationIssues { get; init; } = [];
}

public sealed class SendOrderDebtSummaryResponse
{
    public string Outcome { get; init; } = string.Empty;

    public bool Success { get; init; }

    public string? ErrorMessage { get; init; }

    public DateTime? SentAt { get; init; }

    public string? HistoryId { get; init; }

    public string? EmailProviderMessageId { get; init; }

    public OrderDebtSummarySelectionResponse? Summary { get; init; }

    public IReadOnlyList<OrderDebtSummaryValidationIssueResponse> ValidationIssues { get; init; } = [];
}

public sealed class OrderDebtSummaryValidationIssueResponse
{
    public string LegacyOrderId { get; init; } = string.Empty;

    public string Classification { get; init; } = string.Empty;

    public string ReasonCode { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public bool RequiresReview { get; init; }

    public long? BillingDocumentId { get; init; }

    public long? FiscalDocumentId { get; init; }

    public string? FiscalUuid { get; init; }

    public long? AccountsReceivableInvoiceId { get; init; }

    public string? AccountsReceivableStatus { get; init; }

    public string CurrencyCode { get; init; } = "MXN";

    public decimal? InvoiceTotal { get; init; }

    public decimal? PaidTotal { get; init; }

    public decimal? OutstandingBalance { get; init; }

    public IReadOnlyList<string> RelatedLegacyOrderIds { get; init; } = [];
}

public sealed class OrderDebtSummarySelectionResponse
{
    public int OrderCount { get; init; }

    public decimal? Total { get; init; }

    public List<OrderDebtSummaryTotalByCurrencyResponse> TotalsByCurrency { get; init; } = [];
}

public sealed class OrderDebtSummaryTotalByCurrencyResponse
{
    public string CurrencyCode { get; init; } = "MXN";

    public int OrderCount { get; init; }

    public decimal Total { get; init; }
}

public sealed class OrderDebtSummaryFinalResponse
{
    public List<string> To { get; init; } = [];

    public List<string> Cc { get; init; } = [];

    public List<string> Bcc { get; init; } = [];

    public string Subject { get; init; } = string.Empty;

    public int OrderCount { get; init; }

    public string Format { get; init; } = string.Empty;

    public List<OrderDebtSummaryTotalByCurrencyResponse> TotalsByCurrency { get; init; } = [];
}
