using Pineda.Facturacion.Application.Abstractions.Persistence;

namespace Pineda.Facturacion.Application.UseCases.IssuerProfiles;

public class GetActiveIssuerProfileService
{
    private readonly IIssuerProfileRepository _issuerProfileRepository;

    public GetActiveIssuerProfileService(IIssuerProfileRepository issuerProfileRepository)
    {
        _issuerProfileRepository = issuerProfileRepository;
    }

    public async Task<GetActiveIssuerProfileResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var issuerProfile = await _issuerProfileRepository.GetActiveAsync(cancellationToken);

        return new GetActiveIssuerProfileResult
        {
            Outcome = issuerProfile is null ? GetActiveIssuerProfileOutcome.NotFound : GetActiveIssuerProfileOutcome.Found,
            IsSuccess = issuerProfile is not null,
            IssuerProfile = issuerProfile
        };
    }
}
