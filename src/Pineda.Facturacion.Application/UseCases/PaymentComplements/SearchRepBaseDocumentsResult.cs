namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class SearchRepBaseDocumentsResult
{
    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }

    public int TotalPages { get; init; }

    public IReadOnlyList<RepBaseDocumentUnifiedListItem> Items { get; init; } = [];

    public RepOperationalSummaryCounts SummaryCounts { get; init; } = new();
}
