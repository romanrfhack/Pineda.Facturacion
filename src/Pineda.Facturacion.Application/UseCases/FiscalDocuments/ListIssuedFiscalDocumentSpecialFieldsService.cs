using Pineda.Facturacion.Application.Abstractions.Persistence;

namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public sealed class ListIssuedFiscalDocumentSpecialFieldsService
{
    private readonly IFiscalDocumentRepository _fiscalDocumentRepository;

    public ListIssuedFiscalDocumentSpecialFieldsService(IFiscalDocumentRepository fiscalDocumentRepository)
    {
        _fiscalDocumentRepository = fiscalDocumentRepository;
    }

    public Task<IReadOnlyList<IssuedFiscalDocumentSpecialFieldOption>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        return _fiscalDocumentRepository.GetIssuedSpecialFieldOptionsAsync(cancellationToken);
    }
}
