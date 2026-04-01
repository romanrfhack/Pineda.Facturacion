namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class GetInternalRepBaseDocumentByFiscalDocumentIdResult
{
    public GetInternalRepBaseDocumentByFiscalDocumentIdOutcome Outcome { get; init; }

    public InternalRepBaseDocumentDetail? Document { get; init; }
}
