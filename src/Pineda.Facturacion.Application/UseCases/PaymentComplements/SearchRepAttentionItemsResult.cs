namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class SearchRepAttentionItemsResult
{
    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }

    public int TotalPages { get; init; }

    public IReadOnlyList<RepAttentionItem> Items { get; init; } = [];

    public RepOperationalSummaryCounts SummaryCounts { get; init; } = new();
}
