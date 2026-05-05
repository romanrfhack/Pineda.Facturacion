using Microsoft.AspNetCore.Http.HttpResults;
using Pineda.Facturacion.Application.Security;
using Pineda.Facturacion.Application.UseCases.Reports;

namespace Pineda.Facturacion.Api.Endpoints;

public static class ReportsEndpoints
{
    public static IEndpointRouteBuilder MapReportsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/reports")
            .WithTags("Reports")
            .RequireAuthorization(AuthorizationPolicyNames.AuditRead);

        group.MapGet("/stamped-legacy-notes", SearchStampedLegacyNotesAsync)
            .WithName("SearchStampedLegacyNotesReport")
            .WithSummary("Search stamped legacy notes by Mexico local date range")
            .Produces<StampedLegacyNotesReportListResponse>(StatusCodes.Status200OK)
            .Produces<SimpleErrorResponse>(StatusCodes.Status400BadRequest);

        group.MapGet("/stamped-legacy-notes/export", ExportStampedLegacyNotesAsync)
            .WithName("ExportStampedLegacyNotesReport")
            .WithSummary("Export stamped legacy notes report to XLSX")
            .Produces(StatusCodes.Status200OK, contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
            .Produces<SimpleErrorResponse>(StatusCodes.Status400BadRequest);

        return endpoints;
    }

    private static async Task<Results<Ok<StampedLegacyNotesReportListResponse>, BadRequest<SimpleErrorResponse>>> SearchStampedLegacyNotesAsync(
        DateOnly? fromDate,
        DateOnly? toDate,
        int? page,
        int? pageSize,
        string? receiverSearch,
        string? uuid,
        string? series,
        string? folio,
        string? legacyOrderId,
        string? legacyOrderNumber,
        SearchStampedLegacyNotesReportService service,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateFilters(fromDate, toDate, page, pageSize, validatePaging: true);
        if (validationError is not null)
        {
            return TypedResults.BadRequest(validationError);
        }

        var result = await service.ExecuteAsync(new SearchStampedLegacyNotesReportFilter
        {
            FromDate = fromDate!.Value,
            ToDate = toDate!.Value,
            Page = page ?? SearchStampedLegacyNotesReportService.DefaultPage,
            PageSize = pageSize ?? SearchStampedLegacyNotesReportService.DefaultPageSize,
            ReceiverSearch = receiverSearch,
            Uuid = uuid,
            Series = series,
            Folio = folio,
            LegacyOrderId = legacyOrderId,
            LegacyOrderNumber = legacyOrderNumber
        }, cancellationToken);

        return TypedResults.Ok(new StampedLegacyNotesReportListResponse
        {
            Page = result.Page,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount,
            TotalPages = result.TotalPages,
            Items = result.Items.Select(MapItem).ToList()
        });
    }

    private static async Task<IResult> ExportStampedLegacyNotesAsync(
        DateOnly? fromDate,
        DateOnly? toDate,
        string? receiverSearch,
        string? uuid,
        string? series,
        string? folio,
        string? legacyOrderId,
        string? legacyOrderNumber,
        ExportStampedLegacyNotesReportService service,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateFilters(fromDate, toDate, page: null, pageSize: null, validatePaging: false);
        if (validationError is not null)
        {
            return TypedResults.BadRequest(validationError);
        }

        var result = await service.ExecuteAsync(new SearchStampedLegacyNotesReportFilter
        {
            FromDate = fromDate!.Value,
            ToDate = toDate!.Value,
            ReceiverSearch = receiverSearch,
            Uuid = uuid,
            Series = series,
            Folio = folio,
            LegacyOrderId = legacyOrderId,
            LegacyOrderNumber = legacyOrderNumber
        }, cancellationToken);

        return TypedResults.File(result.Content, result.ContentType, result.FileName);
    }

    private static SimpleErrorResponse? ValidateFilters(
        DateOnly? fromDate,
        DateOnly? toDate,
        int? page,
        int? pageSize,
        bool validatePaging)
    {
        if (!fromDate.HasValue || !toDate.HasValue)
        {
            return new SimpleErrorResponse { ErrorMessage = "La fecha inicial y la fecha final son requeridas." };
        }

        if (fromDate.Value > toDate.Value)
        {
            return new SimpleErrorResponse { ErrorMessage = "La fecha inicial no puede ser mayor a la fecha final." };
        }

        if (!validatePaging)
        {
            return null;
        }

        if (page is < 1)
        {
            return new SimpleErrorResponse { ErrorMessage = "La página debe ser mayor o igual a 1." };
        }

        if (pageSize is < 1 or > SearchStampedLegacyNotesReportService.MaxPageSize)
        {
            return new SimpleErrorResponse
            {
                ErrorMessage = $"El tamaño de página debe estar entre 1 y {SearchStampedLegacyNotesReportService.MaxPageSize}."
            };
        }

        return null;
    }

    private static StampedLegacyNoteReportItemResponse MapItem(StampedLegacyNoteReportItem item)
    {
        return new StampedLegacyNoteReportItemResponse
        {
            StampedAtUtc = item.StampedAtUtc,
            StampedAtLocalText = item.StampedAtLocalText,
            LegacyOrderId = item.LegacyOrderId,
            LegacyOrderNumber = item.LegacyOrderNumber,
            BillingDocumentId = item.BillingDocumentId,
            FiscalDocumentId = item.FiscalDocumentId,
            Series = item.Series,
            Folio = item.Folio,
            Uuid = item.Uuid,
            FiscalStatus = item.FiscalStatus,
            CancellationStatus = item.CancellationStatus,
            ReceiverName = item.ReceiverName,
            ReceiverRfc = item.ReceiverRfc,
            CfdiTotal = item.CfdiTotal,
            NoteAmountInCfdi = item.NoteAmountInCfdi,
            CurrencyCode = item.CurrencyCode,
            ItemCount = item.ItemCount
        };
    }

    public sealed class StampedLegacyNotesReportListResponse
    {
        public int Page { get; init; }
        public int PageSize { get; init; }
        public int TotalCount { get; init; }
        public int TotalPages { get; init; }
        public IReadOnlyList<StampedLegacyNoteReportItemResponse> Items { get; init; } = [];
    }

    public sealed class StampedLegacyNoteReportItemResponse
    {
        public DateTime StampedAtUtc { get; init; }
        public string StampedAtLocalText { get; init; } = string.Empty;
        public string LegacyOrderId { get; init; } = string.Empty;
        public string? LegacyOrderNumber { get; init; }
        public long BillingDocumentId { get; init; }
        public long FiscalDocumentId { get; init; }
        public string? Series { get; init; }
        public string? Folio { get; init; }
        public string Uuid { get; init; } = string.Empty;
        public string FiscalStatus { get; init; } = string.Empty;
        public string? CancellationStatus { get; init; }
        public string ReceiverName { get; init; } = string.Empty;
        public string ReceiverRfc { get; init; } = string.Empty;
        public decimal CfdiTotal { get; init; }
        public decimal NoteAmountInCfdi { get; init; }
        public string CurrencyCode { get; init; } = string.Empty;
        public int ItemCount { get; init; }
    }

    public sealed class SimpleErrorResponse
    {
        public string ErrorMessage { get; init; } = string.Empty;
    }
}
