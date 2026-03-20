using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public class PrepareFiscalDocumentResult
{
    public PrepareFiscalDocumentOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public long BillingDocumentId { get; set; }

    public long? FiscalDocumentId { get; set; }

    public FiscalDocumentStatus? Status { get; set; }
}
