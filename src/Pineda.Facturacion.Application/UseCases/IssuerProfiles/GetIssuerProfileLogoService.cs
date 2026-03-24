using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Abstractions.Storage;

namespace Pineda.Facturacion.Application.UseCases.IssuerProfiles;

public sealed class GetIssuerProfileLogoService
{
    private readonly IIssuerProfileRepository _issuerProfileRepository;
    private readonly IIssuerProfileLogoStorage _logoStorage;

    public GetIssuerProfileLogoService(
        IIssuerProfileRepository issuerProfileRepository,
        IIssuerProfileLogoStorage logoStorage)
    {
        _issuerProfileRepository = issuerProfileRepository;
        _logoStorage = logoStorage;
    }

    public async Task<GetIssuerProfileLogoResult> ExecuteAsync(long issuerProfileId, CancellationToken cancellationToken = default)
    {
        if (issuerProfileId <= 0)
        {
            return NotFound();
        }

        var issuerProfile = await _issuerProfileRepository.GetByIdAsync(issuerProfileId, cancellationToken);
        if (issuerProfile is null || string.IsNullOrWhiteSpace(issuerProfile.LogoStoragePath))
        {
            return NotFound();
        }

        var logo = await _logoStorage.ReadAsync(issuerProfile.LogoStoragePath, cancellationToken);
        if (logo is null)
        {
            return NotFound();
        }

        return new GetIssuerProfileLogoResult
        {
            Outcome = GetIssuerProfileLogoOutcome.Found,
            IsSuccess = true,
            FileName = issuerProfile.LogoFileName ?? logo.FileName,
            ContentType = issuerProfile.LogoContentType ?? logo.ContentType,
            Content = logo.Content
        };
    }

    private static GetIssuerProfileLogoResult NotFound()
    {
        return new GetIssuerProfileLogoResult
        {
            Outcome = GetIssuerProfileLogoOutcome.NotFound,
            IsSuccess = false,
            ErrorMessage = "El logotipo del emisor no está disponible."
        };
    }
}
