using Pineda.Facturacion.Application.Abstractions.Persistence;

namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class GetInternalRepBaseDocumentByFiscalDocumentIdService
{
    private readonly IRepBaseDocumentRepository _repository;

    public GetInternalRepBaseDocumentByFiscalDocumentIdService(IRepBaseDocumentRepository repository)
    {
        _repository = repository;
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

        return new GetInternalRepBaseDocumentByFiscalDocumentIdResult
        {
            Outcome = GetInternalRepBaseDocumentByFiscalDocumentIdOutcome.Found,
            Document = new InternalRepBaseDocumentDetail
            {
                Summary = SearchInternalRepBaseDocumentsService.BuildListItem(document.Summary),
                PaymentApplications = document.PaymentApplications,
                PaymentComplements = document.PaymentComplements
            }
        };
    }
}
