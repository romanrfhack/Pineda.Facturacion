using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Pineda.Facturacion.Application.Abstractions.Storage;
using Pineda.Facturacion.Infrastructure.Options;

namespace Pineda.Facturacion.Infrastructure.Documents;

public sealed class IssuerProfileLogoStorage : IIssuerProfileLogoStorage
{
    private static readonly IReadOnlyDictionary<string, FileSignature> SupportedFormats = new Dictionary<string, FileSignature>(StringComparer.OrdinalIgnoreCase)
    {
        ["image/png"] = new(".png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
        ["image/jpeg"] = new(".jpg", [0xFF, 0xD8, 0xFF]),
        ["image/webp"] = new(".webp", [0x52, 0x49, 0x46, 0x46])
    };

    private readonly string _rootPath;
    private readonly int _maxFileSizeBytes;

    public IssuerProfileLogoStorage(IHostEnvironment hostEnvironment, IOptions<IssuerLogoStorageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(hostEnvironment);
        ArgumentNullException.ThrowIfNull(options);

        var configuredRoot = string.IsNullOrWhiteSpace(options.Value.RootPath)
            ? "App_Data/issuer-logos"
            : options.Value.RootPath.Trim();

        _rootPath = Path.IsPathRooted(configuredRoot)
            ? configuredRoot
            : Path.Combine(hostEnvironment.ContentRootPath, configuredRoot);

        _maxFileSizeBytes = options.Value.MaxFileSizeBytes > 0 ? options.Value.MaxFileSizeBytes : 1_048_576;
    }

    public async Task<StoreIssuerProfileLogoResult> SaveAsync(
        long issuerProfileId,
        string fileName,
        string? declaredContentType,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        if (issuerProfileId <= 0)
        {
            return Failure("El perfil del emisor es obligatorio.");
        }

        if (content.Length == 0)
        {
            return Failure("El archivo del logotipo no puede estar vacío.");
        }

        if (content.Length > _maxFileSizeBytes)
        {
            return Failure($"El logotipo supera el tamaño máximo permitido de {_maxFileSizeBytes / 1024 / 1024.0:0.#} MB.");
        }

        var detectedFormat = DetectFormat(content);
        if (detectedFormat is null)
        {
            return Failure("Solo se permiten imágenes PNG, JPG, JPEG o WEBP válidas.");
        }

        if (!string.IsNullOrWhiteSpace(declaredContentType)
            && !string.Equals(declaredContentType, detectedFormat.ContentType, StringComparison.OrdinalIgnoreCase))
        {
            return Failure("El formato declarado del archivo no coincide con el contenido de la imagen.");
        }

        var fileId = $"{Guid.NewGuid():N}{detectedFormat.Extension}";
        var relativePath = Path.Combine(issuerProfileId.ToString(), fileId).Replace('\\', '/');
        var absolutePath = Path.Combine(_rootPath, issuerProfileId.ToString(), fileId);

        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

        await File.WriteAllBytesAsync(absolutePath, content, cancellationToken);

        var safeFileName = string.IsNullOrWhiteSpace(fileName)
            ? $"logo{detectedFormat.Extension}"
            : Path.GetFileName(fileName);

        return new StoreIssuerProfileLogoResult
        {
            IsSuccess = true,
            StoragePath = relativePath,
            FileName = safeFileName,
            ContentType = detectedFormat.ContentType
        };
    }

    public async Task<IssuerProfileLogoBinary?> ReadAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            return null;
        }

        var absolutePath = Path.Combine(_rootPath, storagePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(absolutePath))
        {
            return null;
        }

        var content = await File.ReadAllBytesAsync(absolutePath, cancellationToken);
        var extension = Path.GetExtension(absolutePath);

        return new IssuerProfileLogoBinary
        {
            Content = content,
            ContentType = ResolveContentType(extension),
            FileName = Path.GetFileName(absolutePath)
        };
    }

    public Task DeleteAsync(string? storagePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            return Task.CompletedTask;
        }

        var absolutePath = Path.Combine(_rootPath, storagePath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
        }

        return Task.CompletedTask;
    }

    private static StoreIssuerProfileLogoResult Failure(string errorMessage)
        => new() { IsSuccess = false, ErrorMessage = errorMessage };

    private static DetectedFormat? DetectFormat(byte[] content)
    {
        if (content.Length >= 12
            && content[0] == 0x52
            && content[1] == 0x49
            && content[2] == 0x46
            && content[3] == 0x46
            && content[8] == 0x57
            && content[9] == 0x45
            && content[10] == 0x42
            && content[11] == 0x50)
        {
            return new DetectedFormat(".webp", "image/webp");
        }

        foreach (var supported in SupportedFormats)
        {
            if (supported.Key.Equals("image/webp", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (content.AsSpan().StartsWith(supported.Value.Signature))
            {
                return new DetectedFormat(supported.Value.Extension, supported.Key);
            }
        }

        return null;
    }

    private static string ResolveContentType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }

    private sealed record FileSignature(string Extension, byte[] Signature);

    private sealed record DetectedFormat(string Extension, string ContentType);
}
