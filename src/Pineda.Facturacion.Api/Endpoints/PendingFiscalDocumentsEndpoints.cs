using Microsoft.AspNetCore.Http.HttpResults;
using Pineda.Facturacion.Api.Security;
using Pineda.Facturacion.Application.Security;
using Pineda.Facturacion.Application.UseCases.FiscalDocuments;

namespace Pineda.Facturacion.Api.Endpoints;

public static class PendingFiscalDocumentsEndpoints
{
    public static IEndpointRouteBuilder MapPendingFiscalDocumentsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/fiscal-documents")
            .WithTags("FiscalDocuments")
            .RequireAuthorization(AuthorizationPolicyNames.Authenticated);

        group.MapGet("/pending-stamping", SearchPendingFiscalDocumentsAsync)
            .WithName("SearchPendingFiscalDocuments")
            .WithSummary("List open billing and fiscal documents that remain pending stamping")
            .WithDescription("Returns draft billing documents whose fiscal snapshot is absent, editable, rejected, or locally discarded, with paging and operational filters.")
            .Produces<PendingFiscalDocumentListResponse>(StatusCodes.Status200OK)
            .Produces<PendingFiscalDocumentErrorResponse>(StatusCodes.Status400BadRequest);

        return endpoints;
    }

    private static async Task<Results<Ok<PendingFiscalDocumentListResponse>, BadRequest<PendingFiscalDocumentErrorResponse>>> SearchPendingFiscalDocumentsAsync(
        int? page,
        int? pageSize,
        string? query,
        string? workStatus,
        string? sort,
        SearchPendingFiscalDocumentsService service,
        CancellationToken cancellationToken)
    {
        if (!TryParseWorkFilter(workStatus, out var parsedWorkFilter))
        {
            return TypedResults.BadRequest(new PendingFiscalDocumentErrorResponse
            {
                ErrorMessage = $"El filtro de estado '{workStatus}' no es válido."
            });
        }

        if (!TryParseSort(sort, out var parsedSort))
        {
            return TypedResults.BadRequest(new PendingFiscalDocumentErrorResponse
            {
                ErrorMessage = $"El ordenamiento '{sort}' no es válido."
            });
        }

        var result = await service.ExecuteAsync(
            new SearchPendingFiscalDocumentsFilter
            {
                Page = page ?? 1,
                PageSize = pageSize ?? 25,
                Query = query,
                WorkFilter = parsedWorkFilter,
                Sort = parsedSort
            },
            cancellationToken);

        return TypedResults.Ok(new PendingFiscalDocumentListResponse
        {
            Page = result.Page,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount,
            TotalPages = result.TotalPages,
            Items = result.Items.Select(MapItem).ToArray()
        });
    }

    private static PendingFiscalDocumentListItemResponse MapItem(PendingFiscalDocumentListItem item)
    {
        return new PendingFiscalDocumentListItemResponse
        {
            BillingDocumentId = item.BillingDocumentId,
            FiscalDocumentId = item.FiscalDocumentId,
            BillingDocumentStatus = item.BillingDocumentStatus,
            FiscalDocumentStatus = item.FiscalDocumentStatus,
            WorkStatus = item.WorkStatus.ToString(),
            WorkStatusLabel = item.WorkStatusLabel,
            RequiresAttention = item.RequiresAttention,
            DocumentType = item.DocumentType,
            Series = item.Series,
            Folio = item.Folio,
            ReceiverName = item.ReceiverName,
            ReceiverRfc = item.ReceiverRfc,
            CurrencyCode = item.CurrencyCode,
            Total = item.Total,
            AssociatedOrderCount = item.AssociatedOrderCount,
            ItemCount = item.ItemCount,
            OrderReferences = item.OrderReferences,
            CreatedAtUtc = EnsureUtc(item.CreatedAtUtc),
            LastActivityAtUtc = EnsureUtc(item.LastActivityAtUtc),
            FiscalPreparedAtUtc = item.FiscalPreparedAtUtc.HasValue
                ? EnsureUtc(item.FiscalPreparedAtUtc.Value)
                : null,
            PaymentMethodSat = item.PaymentMethodSat,
            PaymentFormSat = item.PaymentFormSat
        };
    }

    private static bool TryParseWorkFilter(
        string? value,
        out PendingFiscalDocumentWorkFilter workFilter)
    {
        var normalized = NormalizeKey(value);
        workFilter = normalized switch
        {
            "" or "all" => PendingFiscalDocumentWorkFilter.All,
            "pendingpreparation" => PendingFiscalDocumentWorkFilter.PendingPreparation,
            "readyforstamping" => PendingFiscalDocumentWorkFilter.ReadyForStamping,
            "requiresattention" => PendingFiscalDocumentWorkFilter.RequiresAttention,
            _ => PendingFiscalDocumentWorkFilter.All
        };

        return normalized is "" or "all" or "pendingpreparation" or "readyforstamping" or "requiresattention";
    }

    private static bool TryParseSort(string? value, out PendingFiscalDocumentSort sort)
    {
        var normalized = NormalizeKey(value);
        sort = normalized switch
        {
            "" or "lastactivitydesc" => PendingFiscalDocumentSort.LastActivityDesc,
            "oldestfirst" => PendingFiscalDocumentSort.OldestFirst,
            "totaldesc" => PendingFiscalDocumentSort.TotalDesc,
            _ => PendingFiscalDocumentSort.LastActivityDesc
        };

        return normalized is "" or "lastactivitydesc" or "oldestfirst" or "totaldesc";
    }

    private static string NormalizeKey(string? value)
    {
        return (value ?? string.Empty)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Trim()
            .ToLowerInvariant();
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}

public sealed class PendingFiscalDocumentListResponse
{
    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }

    public int TotalPages { get; init; }

    public IReadOnlyList<PendingFiscalDocumentListItemResponse> Items { get; init; } = [];
}

public sealed class PendingFiscalDocumentListItemResponse
{
    public long BillingDocumentId { get; init; }

    public long? FiscalDocumentId { get; init; }

    public string BillingDocumentStatus { get; init; } = string.Empty;

    public string? FiscalDocumentStatus { get; init; }

    public string WorkStatus { get; init; } = string.Empty;

    public string WorkStatusLabel { get; init; } = string.Empty;

    public bool RequiresAttention { get; init; }

    public string DocumentType { get; init; } = string.Empty;

    public string? Series { get; init; }

    public string? Folio { get; init; }

    public string ReceiverName { get; init; } = string.Empty;

    public string? ReceiverRfc { get; init; }

    public string CurrencyCode { get; init; } = "MXN";

    public decimal Total { get; init; }

    public int AssociatedOrderCount { get; init; }

    public int ItemCount { get; init; }

    public IReadOnlyList<string> OrderReferences { get; init; } = [];

    public DateTime CreatedAtUtc { get; init; }

    public DateTime LastActivityAtUtc { get; init; }

    public DateTime? FiscalPreparedAtUtc { get; init; }

    public string? PaymentMethodSat { get; init; }

    public string? PaymentFormSat { get; init; }
}

public sealed class PendingFiscalDocumentErrorResponse
{
    public string ErrorMessage { get; init; } = string.Empty;
}
