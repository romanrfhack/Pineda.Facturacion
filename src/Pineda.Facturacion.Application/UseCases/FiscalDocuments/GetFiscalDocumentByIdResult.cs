using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public class GetFiscalDocumentByIdResult
{
    public GetFiscalDocumentByIdOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public FiscalDocument? FiscalDocument { get; set; }
}
