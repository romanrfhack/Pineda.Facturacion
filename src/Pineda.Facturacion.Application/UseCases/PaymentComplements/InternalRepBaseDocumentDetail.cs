namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class InternalRepBaseDocumentDetail
{
    public InternalRepBaseDocumentListItem Summary { get; init; } = new();

    public IReadOnlyList<InternalRepBaseDocumentPaymentApplicationReadModel> PaymentApplications { get; init; } = [];

    public IReadOnlyList<InternalRepBaseDocumentPaymentComplementReadModel> PaymentComplements { get; init; } = [];
}
