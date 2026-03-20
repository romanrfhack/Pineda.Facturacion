using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.IssuerProfiles;

public class GetActiveIssuerProfileResult
{
    public GetActiveIssuerProfileOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public IssuerProfile? IssuerProfile { get; set; }
}
