namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public enum GetFiscalDocumentEmailDraftOutcome
{
    Found = 0,
    NotFound = 1,
    NotStamped = 2
}

public class GetFiscalDocumentEmailDraftResult
{
    public GetFiscalDocumentEmailDraftOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public string? DefaultRecipientEmail { get; set; }

    public string? SuggestedSubject { get; set; }

    public string? SuggestedBody { get; set; }

    public string? ErrorMessage { get; set; }
}
