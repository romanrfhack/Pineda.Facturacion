using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Abstractions.Storage;

namespace Pineda.Facturacion.Application.UseCases.IssuerProfiles;

public sealed class RemoveIssuerProfileLogoService
{
    private readonly IIssuerProfileRepository _issuerProfileRepository;
    private readonly IIssuerProfileLogoStorage _logoStorage;
    private readonly IUnitOfWork _unitOfWork;

    public RemoveIssuerProfileLogoService(
        IIssuerProfileRepository issuerProfileRepository,
        IIssuerProfileLogoStorage logoStorage,
        IUnitOfWork unitOfWork)
    {
        _issuerProfileRepository = issuerProfileRepository;
        _logoStorage = logoStorage;
        _unitOfWork = unitOfWork;
    }

    public async Task<RemoveIssuerProfileLogoResult> ExecuteAsync(long issuerProfileId, CancellationToken cancellationToken = default)
    {
        if (issuerProfileId <= 0)
        {
            return NotFound();
        }

        var issuerProfile = await _issuerProfileRepository.GetByIdAsync(issuerProfileId, cancellationToken);
        if (issuerProfile is null)
        {
            return NotFound();
        }

        await _logoStorage.DeleteAsync(issuerProfile.LogoStoragePath, cancellationToken);

        issuerProfile.LogoStoragePath = null;
        issuerProfile.LogoFileName = null;
        issuerProfile.LogoContentType = null;
        issuerProfile.LogoUpdatedAtUtc = null;
        issuerProfile.UpdatedAtUtc = DateTime.UtcNow;

        await _issuerProfileRepository.UpdateAsync(issuerProfile, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new RemoveIssuerProfileLogoResult
        {
            Outcome = RemoveIssuerProfileLogoOutcome.Removed,
            IsSuccess = true,
            IssuerProfileId = issuerProfile.Id
        };
    }

    private static RemoveIssuerProfileLogoResult NotFound()
    {
        return new RemoveIssuerProfileLogoResult
        {
            Outcome = RemoveIssuerProfileLogoOutcome.NotFound,
            IsSuccess = false,
            ErrorMessage = "El perfil del emisor no fue encontrado."
        };
    }
}
