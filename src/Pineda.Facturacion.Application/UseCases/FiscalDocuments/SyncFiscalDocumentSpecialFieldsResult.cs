using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public sealed class SyncFiscalDocumentSpecialFieldsResult
{
    public SyncFiscalDocumentSpecialFieldsOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public long FiscalDocumentId { get; set; }

    public FiscalDocumentStatus? FiscalDocumentStatus { get; set; }

    public int SpecialFieldCount { get; set; }
}
