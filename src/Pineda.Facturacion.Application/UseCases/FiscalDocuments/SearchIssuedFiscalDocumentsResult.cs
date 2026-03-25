namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public sealed class SearchIssuedFiscalDocumentsResult
{
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
    public IReadOnlyList<IssuedFiscalDocumentListItem> Items { get; init; } = [];
}
