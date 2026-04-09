using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public sealed class ReprepareFiscalDocumentResult
{
    public ReprepareFiscalDocumentOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public long FiscalDocumentId { get; set; }

    public long BillingDocumentId { get; set; }

    public FiscalDocumentStatus? FiscalDocumentStatus { get; set; }
}
