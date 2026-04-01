namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class ExternalRepBaseDocumentDetailReadModel
{
    public ExternalRepBaseDocumentSummaryReadModel Summary { get; init; } = new();

    public IReadOnlyList<ExternalRepBaseDocumentPaymentHistoryReadModel> PaymentHistory { get; init; } = [];

    public IReadOnlyList<ExternalRepBaseDocumentPaymentApplicationReadModel> PaymentApplications { get; init; } = [];

    public IReadOnlyList<ExternalRepBaseDocumentPaymentComplementReadModel> PaymentComplements { get; init; } = [];
}
