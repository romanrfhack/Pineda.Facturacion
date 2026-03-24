namespace Pineda.Facturacion.Application.UseCases.IssuerProfiles;

public sealed class RemoveIssuerProfileLogoResult
{
    public RemoveIssuerProfileLogoOutcome Outcome { get; init; }
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public long? IssuerProfileId { get; init; }
}
