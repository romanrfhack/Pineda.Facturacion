using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public class GetFiscalStampByFiscalDocumentIdResult
{
    public GetFiscalStampByFiscalDocumentIdOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public FiscalStamp? FiscalStamp { get; set; }
}
