namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public sealed class SearchIssuedFiscalDocumentsFilter
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    public string? ReceiverRfc { get; init; }
    public string? ReceiverName { get; init; }
    public string? Uuid { get; init; }
    public string? Series { get; init; }
    public string? Folio { get; init; }
    public string? Status { get; init; }
    public string? Query { get; init; }
}
