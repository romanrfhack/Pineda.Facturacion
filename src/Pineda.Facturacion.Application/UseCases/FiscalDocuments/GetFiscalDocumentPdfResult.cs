namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public enum GetFiscalDocumentPdfOutcome
{
    Found = 0,
    NotFound = 1,
    NotStamped = 2
}

public class GetFiscalDocumentPdfResult
{
    public GetFiscalDocumentPdfOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public byte[]? Content { get; set; }

    public string? FileName { get; set; }

    public string? ErrorMessage { get; set; }
}
