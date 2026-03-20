namespace Pineda.Facturacion.Application.UseCases.IssuerProfiles;

public class UpdateIssuerProfileResult
{
    public UpdateIssuerProfileOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public long? IssuerProfileId { get; set; }
}
