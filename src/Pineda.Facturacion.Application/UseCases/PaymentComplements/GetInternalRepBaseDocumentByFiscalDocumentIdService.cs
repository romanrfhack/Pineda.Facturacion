using Pineda.Facturacion.Application.Abstractions.Persistence;

namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class GetInternalRepBaseDocumentByFiscalDocumentIdService
{
    private readonly IRepBaseDocumentRepository _repository;
    private readonly IInternalRepBaseDocumentStateRepository _stateRepository;
    private readonly IUnitOfWork _unitOfWork;

    public GetInternalRepBaseDocumentByFiscalDocumentIdService(
        IRepBaseDocumentRepository repository,
        IInternalRepBaseDocumentStateRepository stateRepository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _stateRepository = stateRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<GetInternalRepBaseDocumentByFiscalDocumentIdResult> ExecuteAsync(
        long fiscalDocumentId,
        CancellationToken cancellationToken = default)
    {
        if (fiscalDocumentId <= 0)
        {
            return new GetInternalRepBaseDocumentByFiscalDocumentIdResult
            {
                Outcome = GetInternalRepBaseDocumentByFiscalDocumentIdOutcome.NotFound
            };
        }

        var document = await _repository.GetInternalByFiscalDocumentIdAsync(fiscalDocumentId, cancellationToken);
        if (document is null)
        {
            return new GetInternalRepBaseDocumentByFiscalDocumentIdResult
            {
                Outcome = GetInternalRepBaseDocumentByFiscalDocumentIdOutcome.NotFound
            };
        }

        var summary = SearchInternalRepBaseDocumentsService.BuildListItem(document.Summary);
        var existingState = await _stateRepository.GetByFiscalDocumentIdAsync(fiscalDocumentId, cancellationToken);
        var stateEntity = InternalRepBaseDocumentOperationalStateProjector.BuildEntity(
            summary,
            summary.Eligibility.EvaluatedAtUtc,
            existingState?.CreatedAtUtc);
        await _stateRepository.UpsertAsync(stateEntity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new GetInternalRepBaseDocumentByFiscalDocumentIdResult
        {
            Outcome = GetInternalRepBaseDocumentByFiscalDocumentIdOutcome.Found,
            Document = new InternalRepBaseDocumentDetail
            {
                Summary = summary,
                OperationalState = InternalRepBaseDocumentOperationalStateProjector.BuildSnapshot(stateEntity),
                Timeline = RepBaseDocumentTimelineBuilder.BuildInternal(
                    summary,
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
