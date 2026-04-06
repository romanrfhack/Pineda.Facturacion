namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class SearchInternalRepBaseDocumentsResult
{
    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }

    public int TotalPages { get; init; }

    public IReadOnlyList<InternalRepBaseDocumentListItem> Items { get; init; } = [];

    public RepOperationalSummaryCounts SummaryCounts { get; init; } = new();
}
