namespace Pineda.Facturacion.Application.Abstractions.Storage;

public interface IIssuerProfileLogoStorage
{
    Task<StoreIssuerProfileLogoResult> SaveAsync(
        long issuerProfileId,
        string fileName,
        string? declaredContentType,
        byte[] content,
        CancellationToken cancellationToken = default);

    Task<IssuerProfileLogoBinary?> ReadAsync(string storagePath, CancellationToken cancellationToken = default);

    Task DeleteAsync(string? storagePath, CancellationToken cancellationToken = default);
}

public sealed class StoreIssuerProfileLogoResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public string StoragePath { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
}

public sealed class IssuerProfileLogoBinary
{
    public byte[] Content { get; init; } = [];
    public string ContentType { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
}
