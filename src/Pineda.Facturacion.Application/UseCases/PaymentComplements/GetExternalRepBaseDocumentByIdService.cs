using Pineda.Facturacion.Application.Abstractions.Persistence;

namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class GetExternalRepBaseDocumentByIdService
{
    private readonly IExternalRepBaseDocumentRepository _repository;
    private readonly IIssuerProfileRepository _issuerProfileRepository;

    public GetExternalRepBaseDocumentByIdService(
        IExternalRepBaseDocumentRepository repository,
        IIssuerProfileRepository issuerProfileRepository)
    {
        _repository = repository;
        _issuerProfileRepository = issuerProfileRepository;
    }

    public async Task<GetExternalRepBaseDocumentByIdResult> ExecuteAsync(long externalRepBaseDocumentId, CancellationToken cancellationToken = default)
    {
        var document = await _repository.GetOperationalByIdAsync(externalRepBaseDocumentId, cancellationToken);
        var activeIssuerProfile = document is null ? null : await _issuerProfileRepository.GetActiveAsync(cancellationToken);
        var summary = document is null ? null : SearchExternalRepBaseDocumentsService.BuildListItem(document.Summary, activeIssuerProfile);

        return new GetExternalRepBaseDocumentByIdResult
        {
            Outcome = document is null ? GetExternalRepBaseDocumentByIdOutcome.NotFound : GetExternalRepBaseDocumentByIdOutcome.Found,
            IsSuccess = document is not null,
            ExternalRepBaseDocumentId = externalRepBaseDocumentId,
            Document = document is null
                ? null
                : new ExternalRepBaseDocumentDetail
                {
                    Summary = summary!,
                    Timeline = RepBaseDocumentTimelineBuilder.BuildExternal(
                        summary!,
                        document.PaymentHistory,
                        document.PaymentApplications,
                        document.PaymentComplements),
                    PaymentHistory = document.PaymentHistory,
                    PaymentApplications = document.PaymentApplications,
                    PaymentComplements = document.PaymentComplements
                }
        };
    }
}
