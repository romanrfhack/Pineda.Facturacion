namespace Pineda.Facturacion.Application.UseCases.IssuerProfiles;

public sealed class GetIssuerProfileLogoResult
{
    public GetIssuerProfileLogoOutcome Outcome { get; init; }
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public byte[] Content { get; init; } = [];
}
