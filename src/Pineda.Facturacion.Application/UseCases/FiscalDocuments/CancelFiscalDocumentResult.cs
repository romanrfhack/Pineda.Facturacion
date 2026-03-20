using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public class CancelFiscalDocumentResult
{
    public CancelFiscalDocumentOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public long FiscalDocumentId { get; set; }

    public FiscalDocumentStatus? FiscalDocumentStatus { get; set; }

    public long? FiscalCancellationId { get; set; }

    public FiscalCancellationStatus? CancellationStatus { get; set; }

    public string? ProviderName { get; set; }

    public string? ProviderTrackingId { get; set; }

    public DateTime? CancelledAtUtc { get; set; }
}
