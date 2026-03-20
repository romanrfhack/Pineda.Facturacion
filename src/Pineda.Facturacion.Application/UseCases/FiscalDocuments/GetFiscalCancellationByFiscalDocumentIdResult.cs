using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public class GetFiscalCancellationByFiscalDocumentIdResult
{
    public GetFiscalCancellationByFiscalDocumentIdOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public FiscalCancellation? FiscalCancellation { get; set; }
}
