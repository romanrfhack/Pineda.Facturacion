namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class SearchExternalRepBaseDocumentsResult
{
    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }

    public int TotalPages { get; init; }

    public IReadOnlyList<ExternalRepBaseDocumentListItem> Items { get; init; } = [];
}
