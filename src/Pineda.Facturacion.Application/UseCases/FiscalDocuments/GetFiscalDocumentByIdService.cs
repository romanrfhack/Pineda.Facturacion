using Pineda.Facturacion.Application.Abstractions.Persistence;

namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public class GetFiscalDocumentByIdService
{
    private readonly IFiscalDocumentRepository _fiscalDocumentRepository;

    public GetFiscalDocumentByIdService(IFiscalDocumentRepository fiscalDocumentRepository)
    {
        _fiscalDocumentRepository = fiscalDocumentRepository;
    }

    public async Task<GetFiscalDocumentByIdResult> ExecuteAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
    {
        var fiscalDocument = await _fiscalDocumentRepository.GetByIdAsync(fiscalDocumentId, cancellationToken);
        return new GetFiscalDocumentByIdResult
        {
            Outcome = fiscalDocument is null ? GetFiscalDocumentByIdOutcome.NotFound : GetFiscalDocumentByIdOutcome.Found,
            IsSuccess = fiscalDocument is not null,
            FiscalDocument = fiscalDocument
        };
    }
}
