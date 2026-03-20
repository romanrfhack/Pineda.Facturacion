using Pineda.Facturacion.Application.Abstractions.Persistence;

namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public class GetFiscalCancellationByFiscalDocumentIdService
{
    private readonly IFiscalCancellationRepository _fiscalCancellationRepository;

    public GetFiscalCancellationByFiscalDocumentIdService(IFiscalCancellationRepository fiscalCancellationRepository)
    {
        _fiscalCancellationRepository = fiscalCancellationRepository;
    }

    public async Task<GetFiscalCancellationByFiscalDocumentIdResult> ExecuteAsync(
        long fiscalDocumentId,
        CancellationToken cancellationToken = default)
    {
        var fiscalCancellation = await _fiscalCancellationRepository.GetByFiscalDocumentIdAsync(fiscalDocumentId, cancellationToken);
        return new GetFiscalCancellationByFiscalDocumentIdResult
        {
            Outcome = fiscalCancellation is null ? GetFiscalCancellationByFiscalDocumentIdOutcome.NotFound : GetFiscalCancellationByFiscalDocumentIdOutcome.Found,
            IsSuccess = fiscalCancellation is not null,
            FiscalCancellation = fiscalCancellation
        };
    }
}
