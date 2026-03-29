using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public class RefreshFiscalDocumentStatusResult
{
    public RefreshFiscalDocumentStatusOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public long FiscalDocumentId { get; set; }

    public FiscalDocumentStatus? FiscalDocumentStatus { get; set; }

    public string? Uuid { get; set; }

    public string? LastKnownExternalStatus { get; set; }

    public string? ProviderCode { get; set; }

    public string? ProviderMessage { get; set; }

    public string? OperationalStatus { get; set; }

    public string? OperationalMessage { get; set; }

    public string? SupportMessage { get; set; }

    public DateTime? CheckedAtUtc { get; set; }
}
