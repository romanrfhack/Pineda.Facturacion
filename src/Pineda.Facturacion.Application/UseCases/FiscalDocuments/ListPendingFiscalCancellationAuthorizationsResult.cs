namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public class ListPendingFiscalCancellationAuthorizationsResult
{
    public ListPendingFiscalCancellationAuthorizationsOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public string? ProviderName { get; set; }

    public string? ProviderCode { get; set; }

    public string? ProviderMessage { get; set; }

    public string? SupportMessage { get; set; }

    public string? RawResponseSummaryJson { get; set; }

    public IReadOnlyList<PendingFiscalCancellationAuthorizationItem> Items { get; set; } = [];
}
