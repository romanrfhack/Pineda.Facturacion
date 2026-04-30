using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Abstractions.Storage;

namespace Pineda.Facturacion.Application.UseCases.IssuerProfiles;

public sealed class UploadIssuerProfileLogoService
{
    private const int MaxLogoFileSizeBytes = 1_048_576;

    private static readonly IReadOnlyDictionary<string, LogoFormat> SupportedFormats = new Dictionary<string, LogoFormat>(StringComparer.OrdinalIgnoreCase)
    {
        ["image/png"] = new(".png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
        ["image/jpeg"] = new(".jpg", [0xFF, 0xD8, 0xFF])
    };

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

        var validation = ValidateLogo(fileName, declaredContentType, content);
        if (!validation.IsSuccess || validation.Format is null)
        {
            return ValidationFailure(validation.ErrorMessage ?? "No fue posible validar el logotipo.");
        }

        var previousStoragePath = issuerProfile.LogoStoragePath;
        issuerProfile.LogoStoragePath = null;
        issuerProfile.LogoData = content.ToArray();
        issuerProfile.LogoSizeBytes = content.Length;
        issuerProfile.LogoFileName = validation.FileName;
        issuerProfile.LogoContentType = validation.Format.ContentType;
        issuerProfile.LogoUpdatedAtUtc = DateTime.UtcNow;
        issuerProfile.UpdatedAtUtc = DateTime.UtcNow;

        await _issuerProfileRepository.UpdateAsync(issuerProfile, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(previousStoragePath))
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

    private static LogoValidationResult ValidateLogo(string fileName, string? declaredContentType, byte[] content)
    {
        if (content.Length == 0)
        {
            return LogoValidationResult.Failure("El archivo del logotipo no puede estar vacío.");
        }

        if (content.Length > MaxLogoFileSizeBytes)
        {
            return LogoValidationResult.Failure("El logotipo supera el tamaño máximo permitido de 1 MB.");
        }

        var detectedFormat = DetectFormat(content);
        if (detectedFormat is null)
        {
            return LogoValidationResult.Failure("Solo se permiten imágenes PNG, JPG o JPEG válidas.");
        }

        if (!string.IsNullOrWhiteSpace(declaredContentType)
            && !string.Equals(declaredContentType, detectedFormat.ContentType, StringComparison.OrdinalIgnoreCase))
        {
            return LogoValidationResult.Failure("El formato declarado del archivo no coincide con el contenido de la imagen.");
        }

        var safeFileName = NormalizeFileName(fileName, detectedFormat.Extension);

        return LogoValidationResult.Success(detectedFormat, safeFileName);
    }

    private static string NormalizeFileName(string fileName, string extension)
    {
        var candidate = string.IsNullOrWhiteSpace(fileName)
            ? string.Empty
            : Path.GetFileName(fileName.Replace('\\', '/')).Trim();

        if (string.IsNullOrWhiteSpace(candidate) || candidate is "." or "..")
        {
            return $"logo{extension}";
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(candidate
            .Select(character => invalidChars.Contains(character) || char.IsControl(character) ? '_' : character)
            .ToArray());

        return string.IsNullOrWhiteSpace(sanitized) ? $"logo{extension}" : sanitized;
    }

    private static LogoFormat? DetectFormat(byte[] content)
    {
        foreach (var supported in SupportedFormats)
        {
            if (content.AsSpan().StartsWith(supported.Value.Signature))
            {
                return supported.Value with { ContentType = supported.Key };
            }
        }

        return null;
    }

    private sealed record LogoFormat(string Extension, byte[] Signature)
    {
        public string ContentType { get; init; } = string.Empty;
    }

    private sealed class LogoValidationResult
    {
        public bool IsSuccess { get; private init; }
        public string? ErrorMessage { get; private init; }
        public LogoFormat? Format { get; private init; }
        public string FileName { get; private init; } = string.Empty;

        public static LogoValidationResult Success(LogoFormat format, string fileName)
            => new()
            {
                IsSuccess = true,
                Format = format,
                FileName = fileName
            };

        public static LogoValidationResult Failure(string errorMessage)
            => new()
            {
                IsSuccess = false,
                ErrorMessage = errorMessage
            };
    }
}
