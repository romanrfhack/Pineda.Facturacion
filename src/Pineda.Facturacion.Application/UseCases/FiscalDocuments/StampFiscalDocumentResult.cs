using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public class StampFiscalDocumentResult
{
    public StampFiscalDocumentOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public long FiscalDocumentId { get; set; }

    public FiscalDocumentStatus? FiscalDocumentStatus { get; set; }

    public long? FiscalStampId { get; set; }

    public string? Uuid { get; set; }

    public DateTime? StampedAtUtc { get; set; }

    public string? ProviderName { get; set; }

    public string? ProviderTrackingId { get; set; }
}
