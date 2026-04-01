using Microsoft.AspNetCore.Http.HttpResults;
using Pineda.Facturacion.Api.Security;
using Pineda.Facturacion.Application.Abstractions.Security;
using Pineda.Facturacion.Application.UseCases.ImportLegacyOrder;
using Pineda.Facturacion.Application.UseCases.ImportLegacyOrderPreview;
using Pineda.Facturacion.Application.UseCases.Orders;
using Pineda.Facturacion.Application.Security;

namespace Pineda.Facturacion.Api.Endpoints;

public static class OrdersEndpoints
{
    private const string LegacySourceSystem = "legacy";
    private const string LegacyOrdersSourceTable = "pedidos";

    public static IEndpointRouteBuilder MapOrdersEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/orders")
            .WithTags("Orders")
            .RequireAuthorization(AuthorizationPolicyNames.OperatorOrAbove);

        group.MapGet("/legacy", SearchLegacyOrdersAsync)
            .WithName("SearchLegacyOrders")
            .WithSummary("Search legacy orders by date range")
            .WithDescription("Queries legacy orders in read-only mode using a bounded date range and paged results.")
            .Produces<SearchLegacyOrdersResponse>(StatusCodes.Status200OK)
            .Produces<SearchLegacyOrdersResponse>(StatusCodes.Status400BadRequest);

        group.MapPost("/{legacyOrderId}/import", ImportOrderAsync)
            .WithName("ImportLegacyOrder")
            .WithSummary("Import a legacy order snapshot")
            .WithDescription("Reads a single legacy order, creates a snapshot in the billing database, and returns the import result.")
            .Produces<ImportLegacyOrderResponse>(StatusCodes.Status200OK)
            .Produces<ImportLegacyOrderResponse>(StatusCodes.Status404NotFound)
            .Produces<ImportLegacyOrderResponse>(StatusCodes.Status409Conflict);

        group.MapGet("/{legacyOrderId}/import-preview", PreviewImportAsync)
            .WithName("PreviewLegacyOrderImport")
            .WithSummary("Preview changes for a previously imported legacy order")
            .WithDescription("Reads the current legacy order, compares it against the existing internal snapshot, and returns a non-destructive preview plus reimport eligibility.")
            .Produces<ImportLegacyOrderPreviewResponse>(StatusCodes.Status200OK)
            .Produces<ImportLegacyOrderPreviewResponse>(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<Results<Ok<SearchLegacyOrdersResponse>, BadRequest<SearchLegacyOrdersResponse>>> SearchLegacyOrdersAsync(
        DateOnly? fromDate,
        DateOnly? toDate,
        string? customerQuery,
        int? page,
        int? pageSize,
        SearchLegacyOrdersService service,
        CancellationToken cancellationToken)
    {
        if (fromDate is null || toDate is null)
        {
            return TypedResults.BadRequest(CreateValidationFailure("La fecha inicial y la fecha final son obligatorias."));
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (fromDate > toDate)
        {
            return TypedResults.BadRequest(CreateValidationFailure("La fecha inicial no puede ser mayor que la fecha final."));
        }

        if (toDate > today)
        {
            return TypedResults.BadRequest(CreateValidationFailure("La fecha final no puede ser mayor al día actual."));
        }

        var normalizedPage = page.GetValueOrDefault(1);
        var normalizedPageSize = pageSize.GetValueOrDefault(10);

        if (normalizedPage < 1)
        {
            return TypedResults.BadRequest(CreateValidationFailure("La página debe ser mayor o igual a 1."));
        }

        if (normalizedPageSize is < 1 or > 10)
        {
            return TypedResults.BadRequest(CreateValidationFailure("El tamaño de página debe estar entre 1 y 10."));
        }

        var result = await service.ExecuteAsync(
            new SearchLegacyOrdersFilter
            {
                FromDateUtc = fromDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                ToDateUtcExclusive = toDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                CustomerQuery = customerQuery,
                Page = normalizedPage,
                PageSize = normalizedPageSize
            },
            cancellationToken);

        return TypedResults.Ok(new SearchLegacyOrdersResponse
        {
            IsSuccess = true,
            Page = result.Page,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount,
            TotalPages = result.TotalCount == 0 ? 0 : (int)Math.Ceiling(result.TotalCount / (double)result.PageSize),
            Items = result.Items.Select(item => new SearchLegacyOrderItemResponse
            {
                LegacyOrderId = item.LegacyOrderId,
                OrderDateUtc = item.OrderDateUtc,
                CustomerName = item.CustomerName,
                Total = item.Total,
                LegacyOrderType = item.LegacyOrderType,
                IsImported = item.IsImported,
                SalesOrderId = item.SalesOrderId,
                BillingDocumentId = item.BillingDocumentId,
                BillingDocumentStatus = item.BillingDocumentStatus,
                FiscalDocumentId = item.FiscalDocumentId,
                FiscalDocumentStatus = item.FiscalDocumentStatus,
                ImportStatus = item.ImportStatus
            }).ToArray()
        });
    }

    private static async Task<Results<Ok<ImportLegacyOrderResponse>, NotFound<ImportLegacyOrderResponse>, Conflict<ImportLegacyOrderResponse>>> ImportOrderAsync(
        string legacyOrderId,
        ImportLegacyOrderService service,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(
            new ImportLegacyOrderCommand
            {
                // Source system naming is not formalized in docs yet, so this stays as an explicit local assumption.
                SourceSystem = LegacySourceSystem,
                SourceTable = LegacyOrdersSourceTable,
                LegacyOrderId = legacyOrderId
            },
            cancellationToken);

        var response = new ImportLegacyOrderResponse
        {
            Outcome = result.Outcome.ToString(),
            IsSuccess = result.IsSuccess,
            IsIdempotent = result.IsIdempotent,
            ErrorMessage = result.ErrorMessage,
            ErrorCode = result.ErrorCode,
            SourceSystem = result.SourceSystem,
            SourceTable = result.SourceTable,
            LegacyOrderId = result.LegacyOrderId,
            SourceHash = result.SourceHash,
            LegacyImportRecordId = result.LegacyImportRecordId,
            SalesOrderId = result.SalesOrderId,
            ImportStatus = result.ImportStatus?.ToString(),
            ExistingSalesOrderId = result.ExistingSalesOrderId,
            ExistingSalesOrderStatus = result.ExistingSalesOrderStatus,
            ExistingBillingDocumentId = result.ExistingBillingDocumentId,
            ExistingBillingDocumentStatus = result.ExistingBillingDocumentStatus,
            ExistingFiscalDocumentId = result.ExistingFiscalDocumentId,
            ExistingFiscalDocumentStatus = result.ExistingFiscalDocumentStatus,
            FiscalUuid = result.FiscalUuid,
            ImportedAtUtc = result.ImportedAtUtc,
            ExistingSourceHash = result.ExistingSourceHash,
            CurrentSourceHash = result.CurrentSourceHash,
            AllowedActions = result.AllowedActions
        };

        await AuditApiHelper.RecordAsync(
            auditService,
            "Order.Import",
            "SalesOrder",
            result.SalesOrderId?.ToString() ?? legacyOrderId,
            result.Outcome.ToString(),
            new { legacyOrderId, result.SourceSystem, result.SourceTable },
            new { result.SalesOrderId, result.LegacyImportRecordId, result.ImportStatus },
            result.ErrorMessage,
            cancellationToken);

        return result.Outcome switch
        {
            ImportLegacyOrderOutcome.Imported => TypedResults.Ok(response),
            ImportLegacyOrderOutcome.Idempotent => TypedResults.Ok(response),
            ImportLegacyOrderOutcome.NotFound => TypedResults.NotFound(response),
            ImportLegacyOrderOutcome.Conflict => TypedResults.Conflict(response),
            _ => TypedResults.Conflict(response)
        };
    }

    private static async Task<Results<Ok<ImportLegacyOrderPreviewResponse>, NotFound<ImportLegacyOrderPreviewResponse>>> PreviewImportAsync(
        string legacyOrderId,
        PreviewLegacyOrderImportService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(legacyOrderId, cancellationToken);
        var response = new ImportLegacyOrderPreviewResponse
        {
            IsSuccess = result.IsSuccess,
            ErrorMessage = result.ErrorMessage,
            LegacyOrderId = result.LegacyOrderId,
            ExistingSalesOrderId = result.ExistingSalesOrderId,
            ExistingSalesOrderStatus = result.ExistingSalesOrderStatus,
            ExistingBillingDocumentId = result.ExistingBillingDocumentId,
            ExistingBillingDocumentStatus = result.ExistingBillingDocumentStatus,
            ExistingFiscalDocumentId = result.ExistingFiscalDocumentId,
            ExistingFiscalDocumentStatus = result.ExistingFiscalDocumentStatus,
            FiscalUuid = result.FiscalUuid,
            ExistingSourceHash = result.ExistingSourceHash,
            CurrentSourceHash = result.CurrentSourceHash,
            HasChanges = result.HasChanges,
            ChangedOrderFields = result.ChangedOrderFields,
            ChangeSummary = new ImportLegacyOrderPreviewChangeSummaryResponse
            {
                AddedLines = result.ChangeSummary.AddedLines,
                RemovedLines = result.ChangeSummary.RemovedLines,
                ModifiedLines = result.ChangeSummary.ModifiedLines,
                UnchangedLines = result.ChangeSummary.UnchangedLines,
                OldSubtotal = result.ChangeSummary.OldSubtotal,
                NewSubtotal = result.ChangeSummary.NewSubtotal,
                OldTotal = result.ChangeSummary.OldTotal,
                NewTotal = result.ChangeSummary.NewTotal
            },
            LineChanges = result.LineChanges.Select(change => new ImportLegacyOrderPreviewLineChangeResponse
            {
                ChangeType = change.ChangeType.ToString(),
                MatchKey = change.MatchKey,
                OldLine = MapLine(change.OldLine),
                NewLine = MapLine(change.NewLine),
                ChangedFields = change.ChangedFields
            }).ToArray(),
            ReimportEligibility = new ImportLegacyOrderPreviewEligibilityResponse
            {
                Status = result.ReimportEligibility.Status.ToString(),
                ReasonCode = result.ReimportEligibility.ReasonCode.ToString(),
                ReasonMessage = result.ReimportEligibility.ReasonMessage
            },
            AllowedActions = result.AllowedActions
        };

        return result.IsSuccess
            ? TypedResults.Ok(response)
            : TypedResults.NotFound(response);
    }

    public sealed class ImportLegacyOrderResponse
    {
        public string Outcome { get; init; } = string.Empty;

        public bool IsSuccess { get; init; }

        public bool IsIdempotent { get; init; }

        public string? ErrorMessage { get; init; }

        public string? ErrorCode { get; init; }

        public string SourceSystem { get; init; } = string.Empty;

        public string SourceTable { get; init; } = string.Empty;

        public string LegacyOrderId { get; init; } = string.Empty;

        public string SourceHash { get; init; } = string.Empty;

        public long? LegacyImportRecordId { get; init; }

        public long? SalesOrderId { get; init; }

        public string? ImportStatus { get; init; }

        public long? ExistingSalesOrderId { get; init; }

        public string? ExistingSalesOrderStatus { get; init; }

        public long? ExistingBillingDocumentId { get; init; }

        public string? ExistingBillingDocumentStatus { get; init; }

        public long? ExistingFiscalDocumentId { get; init; }

        public string? ExistingFiscalDocumentStatus { get; init; }

        public string? FiscalUuid { get; init; }

        public DateTime? ImportedAtUtc { get; init; }

        public string? ExistingSourceHash { get; init; }

        public string? CurrentSourceHash { get; init; }

        public IReadOnlyList<string> AllowedActions { get; init; } = [];
    }

    private static SearchLegacyOrdersResponse CreateValidationFailure(string errorMessage)
    {
        return new SearchLegacyOrdersResponse
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            Items = [],
            Page = 1,
            PageSize = 10,
            TotalCount = 0,
            TotalPages = 0
        };
    }

    public sealed class SearchLegacyOrdersResponse
    {
        public bool IsSuccess { get; init; }

        public string? ErrorMessage { get; init; }

        public IReadOnlyList<SearchLegacyOrderItemResponse> Items { get; init; } = [];

        public int TotalCount { get; init; }

        public int TotalPages { get; init; }

        public int Page { get; init; }

        public int PageSize { get; init; }
    }

    public sealed class SearchLegacyOrderItemResponse
    {
        public string LegacyOrderId { get; init; } = string.Empty;

        public DateTime OrderDateUtc { get; init; }

        public string CustomerName { get; init; } = string.Empty;

        public decimal Total { get; init; }

        public string? LegacyOrderType { get; init; }

        public bool IsImported { get; init; }

        public long? SalesOrderId { get; init; }

        public long? BillingDocumentId { get; init; }

        public string? BillingDocumentStatus { get; init; }

        public long? FiscalDocumentId { get; init; }

        public string? FiscalDocumentStatus { get; init; }

        public string? ImportStatus { get; init; }
    }

    public sealed class ImportLegacyOrderPreviewResponse
    {
        public bool IsSuccess { get; init; }

        public string? ErrorMessage { get; init; }

        public string LegacyOrderId { get; init; } = string.Empty;

        public long? ExistingSalesOrderId { get; init; }

        public string? ExistingSalesOrderStatus { get; init; }

        public long? ExistingBillingDocumentId { get; init; }

        public string? ExistingBillingDocumentStatus { get; init; }

        public long? ExistingFiscalDocumentId { get; init; }

        public string? ExistingFiscalDocumentStatus { get; init; }

        public string? FiscalUuid { get; init; }

        public string ExistingSourceHash { get; init; } = string.Empty;

        public string CurrentSourceHash { get; init; } = string.Empty;

        public bool HasChanges { get; init; }

        public IReadOnlyList<string> ChangedOrderFields { get; init; } = [];

        public ImportLegacyOrderPreviewChangeSummaryResponse ChangeSummary { get; init; } = new();

        public IReadOnlyList<ImportLegacyOrderPreviewLineChangeResponse> LineChanges { get; init; } = [];

        public ImportLegacyOrderPreviewEligibilityResponse ReimportEligibility { get; init; } = new();

        public IReadOnlyList<string> AllowedActions { get; init; } = [];
    }

    public sealed class ImportLegacyOrderPreviewChangeSummaryResponse
    {
        public int AddedLines { get; init; }

        public int RemovedLines { get; init; }

        public int ModifiedLines { get; init; }

        public int UnchangedLines { get; init; }

        public decimal OldSubtotal { get; init; }

        public decimal NewSubtotal { get; init; }

        public decimal OldTotal { get; init; }

        public decimal NewTotal { get; init; }
    }

    public sealed class ImportLegacyOrderPreviewLineChangeResponse
    {
        public string ChangeType { get; init; } = string.Empty;

        public string MatchKey { get; init; } = string.Empty;

        public ImportLegacyOrderPreviewLineResponse? OldLine { get; init; }

        public ImportLegacyOrderPreviewLineResponse? NewLine { get; init; }

        public IReadOnlyList<string> ChangedFields { get; init; } = [];
    }

    public sealed class ImportLegacyOrderPreviewLineResponse
    {
        public int LineNumber { get; init; }

        public string LegacyArticleId { get; init; } = string.Empty;

        public string? Sku { get; init; }

        public string Description { get; init; } = string.Empty;

        public string? UnitCode { get; init; }

        public string? UnitName { get; init; }

        public decimal Quantity { get; init; }

        public decimal UnitPrice { get; init; }

        public decimal DiscountAmount { get; init; }

        public decimal TaxAmount { get; init; }

        public decimal LineTotal { get; init; }
    }

    public sealed class ImportLegacyOrderPreviewEligibilityResponse
    {
        public string Status { get; init; } = string.Empty;

        public string ReasonCode { get; init; } = string.Empty;

        public string ReasonMessage { get; init; } = string.Empty;
    }

    private static ImportLegacyOrderPreviewLineResponse? MapLine(PreviewLegacyOrderLineSnapshot? line)
    {
        if (line is null)
        {
            return null;
        }

        return new ImportLegacyOrderPreviewLineResponse
        {
            LineNumber = line.LineNumber,
            LegacyArticleId = line.LegacyArticleId,
            Sku = line.Sku,
            Description = line.Description,
            UnitCode = line.UnitCode,
            UnitName = line.UnitName,
            Quantity = line.Quantity,
            UnitPrice = line.UnitPrice,
            DiscountAmount = line.DiscountAmount,
            TaxAmount = line.TaxAmount,
            LineTotal = line.LineTotal
        };
    }
}
