using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class GetExternalRepBaseDocumentByIdResult
{
    public GetExternalRepBaseDocumentByIdOutcome Outcome { get; init; }

    public bool IsSuccess { get; init; }

    public long ExternalRepBaseDocumentId { get; init; }

    public ExternalRepBaseDocument? Document { get; init; }
}
