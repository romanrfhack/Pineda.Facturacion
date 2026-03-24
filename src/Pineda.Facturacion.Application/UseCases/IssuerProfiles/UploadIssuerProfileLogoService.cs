using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Abstractions.Storage;

namespace Pineda.Facturacion.Application.UseCases.IssuerProfiles;

public sealed class UploadIssuerProfileLogoService
{
    private readonly IIssuerProfileRepository _issuerProfileRepository;
    private readonly IIssuerProfileLogoStorage _logoStorage;
    private readonly IUnitOfWork _unitOfWork;

    public UploadIssuerProfileLogoService(
        IIssuerProfileRepository issuerProfileRepository,
        IIssuerProfileLogoStorage logoStorage,
        IUnitOfWork unitOfWork)
    {
        _issuerProfileRepository = issuerProfileRepository;
        _logoStorage = logoStorage;
        _unitOfWork = unitOfWork;
    }

    public async Task<UploadIssuerProfileLogoResult> ExecuteAsync(
        long issuerProfileId,
        string fileName,
        string? declaredContentType,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        if (issuerProfileId <= 0)
        {
            return ValidationFailure("El perfil del emisor es obligatorio.");
        }

        var issuerProfile = await _issuerProfileRepository.GetByIdAsync(issuerProfileId, cancellationToken);
        if (issuerProfile is null)
        {
            return new UploadIssuerProfileLogoResult
            {
                Outcome = UploadIssuerProfileLogoOutcome.NotFound,
                IsSuccess = false,
                ErrorMessage = $"Issuer profile '{issuerProfileId}' was not found."
            };
        }

        var storageResult = await _logoStorage.SaveAsync(issuerProfileId, fileName, declaredContentType, content, cancellationToken);
        if (!storageResult.IsSuccess)
        {
            return ValidationFailure(storageResult.ErrorMessage ?? "No fue posible guardar el logotipo.");
        }

        var previousStoragePath = issuerProfile.LogoStoragePath;
        try
        {
            issuerProfile.LogoStoragePath = storageResult.StoragePath;
            issuerProfile.LogoFileName = storageResult.FileName;
            issuerProfile.LogoContentType = storageResult.ContentType;
            issuerProfile.LogoUpdatedAtUtc = DateTime.UtcNow;
            issuerProfile.UpdatedAtUtc = DateTime.UtcNow;

            await _issuerProfileRepository.UpdateAsync(issuerProfile, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await _logoStorage.DeleteAsync(storageResult.StoragePath, cancellationToken);
            throw;
        }

        if (!string.Equals(previousStoragePath, storageResult.StoragePath, StringComparison.OrdinalIgnoreCase))
        {
            await _logoStorage.DeleteAsync(previousStoragePath, cancellationToken);
        }

        return new UploadIssuerProfileLogoResult
        {
            Outcome = UploadIssuerProfileLogoOutcome.Updated,
            IsSuccess = true,
            IssuerProfileId = issuerProfile.Id
        };
    }

    private static UploadIssuerProfileLogoResult ValidationFailure(string errorMessage)
    {
        return new UploadIssuerProfileLogoResult
        {
            Outcome = UploadIssuerProfileLogoOutcome.ValidationFailed,
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }
}
