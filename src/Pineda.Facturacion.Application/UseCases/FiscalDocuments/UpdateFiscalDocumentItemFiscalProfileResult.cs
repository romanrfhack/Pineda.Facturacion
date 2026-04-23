using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public sealed class UpdateFiscalDocumentItemFiscalProfileResult
{
    public UpdateFiscalDocumentItemFiscalProfileOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public long FiscalDocumentId { get; set; }

    public long FiscalDocumentItemId { get; set; }

    public FiscalDocumentStatus? FiscalDocumentStatus { get; set; }

    public FiscalDocumentItem? FiscalDocumentItem { get; set; }
}
