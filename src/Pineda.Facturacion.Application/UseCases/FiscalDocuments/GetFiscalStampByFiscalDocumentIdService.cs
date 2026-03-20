using Pineda.Facturacion.Application.Abstractions.Persistence;

namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public class GetFiscalStampByFiscalDocumentIdService
{
    private readonly IFiscalStampRepository _fiscalStampRepository;

    public GetFiscalStampByFiscalDocumentIdService(IFiscalStampRepository fiscalStampRepository)
    {
        _fiscalStampRepository = fiscalStampRepository;
    }

    public async Task<GetFiscalStampByFiscalDocumentIdResult> ExecuteAsync(
        long fiscalDocumentId,
        CancellationToken cancellationToken = default)
    {
        var fiscalStamp = await _fiscalStampRepository.GetByFiscalDocumentIdAsync(fiscalDocumentId, cancellationToken);
        return new GetFiscalStampByFiscalDocumentIdResult
        {
            Outcome = fiscalStamp is null ? GetFiscalStampByFiscalDocumentIdOutcome.NotFound : GetFiscalStampByFiscalDocumentIdOutcome.Found,
            IsSuccess = fiscalStamp is not null,
            FiscalStamp = fiscalStamp
        };
    }
}
