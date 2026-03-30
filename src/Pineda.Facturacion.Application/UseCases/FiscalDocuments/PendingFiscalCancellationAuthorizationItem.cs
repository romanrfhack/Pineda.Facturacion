namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public class PendingFiscalCancellationAuthorizationItem
{
    public string Uuid { get; set; } = string.Empty;

    public string? IssuerRfc { get; set; }

    public string? ReceiverRfc { get; set; }

    public string? ProviderCode { get; set; }

    public string? ProviderMessage { get; set; }

    public DateTime? RequestedAtUtc { get; set; }

    public long? FiscalDocumentId { get; set; }

    public string? FiscalDocumentStatus { get; set; }

    public long? FiscalCancellationId { get; set; }

    public string? CancellationStatus { get; set; }

    public string? AuthorizationStatus { get; set; }

    public string? LocalOperationalStatus { get; set; }

    public string? LocalOperationalMessage { get; set; }

    public string? RawItemSummaryJson { get; set; }
}
