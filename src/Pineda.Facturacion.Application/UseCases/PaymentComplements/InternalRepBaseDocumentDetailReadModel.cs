namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class InternalRepBaseDocumentDetailReadModel
{
    public InternalRepBaseDocumentSummaryReadModel Summary { get; init; } = new();

    public IReadOnlyList<InternalRepBaseDocumentPaymentHistoryReadModel> PaymentHistory { get; init; } = [];

    public IReadOnlyList<InternalRepBaseDocumentPaymentApplicationReadModel> PaymentApplications { get; init; } = [];

    public IReadOnlyList<InternalRepBaseDocumentPaymentComplementReadModel> PaymentComplements { get; init; } = [];
}
