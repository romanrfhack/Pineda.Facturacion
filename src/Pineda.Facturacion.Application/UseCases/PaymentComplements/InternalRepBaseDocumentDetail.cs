namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class InternalRepBaseDocumentDetail
{
    public InternalRepBaseDocumentListItem Summary { get; init; } = new();

    public InternalRepBaseDocumentOperationalSnapshot? OperationalState { get; init; }

    public IReadOnlyList<InternalRepBaseDocumentPaymentHistoryReadModel> PaymentHistory { get; init; } = [];

    public IReadOnlyList<InternalRepBaseDocumentPaymentApplicationReadModel> PaymentApplications { get; init; } = [];

    public IReadOnlyList<InternalRepBaseDocumentPaymentComplementReadModel> PaymentComplements { get; init; } = [];
}
