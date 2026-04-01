using Pineda.Facturacion.Application.Abstractions.Persistence;

namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class GetExternalRepBaseDocumentByIdService
{
    private readonly IExternalRepBaseDocumentRepository _repository;

    public GetExternalRepBaseDocumentByIdService(IExternalRepBaseDocumentRepository repository)
    {
        _repository = repository;
    }

    public async Task<GetExternalRepBaseDocumentByIdResult> ExecuteAsync(long externalRepBaseDocumentId, CancellationToken cancellationToken = default)
    {
        var document = await _repository.GetByIdAsync(externalRepBaseDocumentId, cancellationToken);
        return new GetExternalRepBaseDocumentByIdResult
        {
            Outcome = document is null ? GetExternalRepBaseDocumentByIdOutcome.NotFound : GetExternalRepBaseDocumentByIdOutcome.Found,
            IsSuccess = document is not null,
            ExternalRepBaseDocumentId = externalRepBaseDocumentId,
            Document = document
        };
    }
}
