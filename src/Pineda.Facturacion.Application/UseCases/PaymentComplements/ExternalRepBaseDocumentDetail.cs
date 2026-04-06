namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class ExternalRepBaseDocumentDetail
{
    public ExternalRepBaseDocumentListItem Summary { get; init; } = new();

    public IReadOnlyList<RepBaseDocumentTimelineEntry> Timeline { get; init; } = [];

    public IReadOnlyList<ExternalRepBaseDocumentPaymentHistoryReadModel> PaymentHistory { get; init; } = [];

    public IReadOnlyList<ExternalRepBaseDocumentPaymentApplicationReadModel> PaymentApplications { get; init; } = [];

    public IReadOnlyList<ExternalRepBaseDocumentPaymentComplementReadModel> PaymentComplements { get; init; } = [];
}
