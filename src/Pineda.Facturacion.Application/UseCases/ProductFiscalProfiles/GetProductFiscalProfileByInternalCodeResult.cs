using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.ProductFiscalProfiles;

public class GetProductFiscalProfileByInternalCodeResult
{
    public GetProductFiscalProfileByInternalCodeOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public ProductFiscalProfile? ProductFiscalProfile { get; set; }
}
