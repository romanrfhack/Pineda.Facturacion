namespace Pineda.Facturacion.Application.UseCases.ProductFiscalProfiles;

public sealed class ApproveLegacySatAssignmentResult
{
    public ApproveLegacySatAssignmentOutcome Outcome { get; init; }

    public bool IsSuccess { get; init; }

    public string? ErrorMessage { get; init; }

    public long? ProductFiscalProfileId { get; init; }
}
