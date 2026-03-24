namespace Pineda.Facturacion.Application.UseCases.IssuerProfiles;

public sealed class UploadIssuerProfileLogoResult
{
    public UploadIssuerProfileLogoOutcome Outcome { get; init; }
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public long? IssuerProfileId { get; init; }
}
