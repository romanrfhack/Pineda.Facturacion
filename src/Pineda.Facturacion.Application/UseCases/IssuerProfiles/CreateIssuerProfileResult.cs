namespace Pineda.Facturacion.Application.UseCases.IssuerProfiles;

public class CreateIssuerProfileResult
{
    public CreateIssuerProfileOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public long? IssuerProfileId { get; set; }
}
