namespace Pineda.Facturacion.Application.UseCases.ProductFiscalProfiles;

public class UpdateProductFiscalProfileResult
{
    public UpdateProductFiscalProfileOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public long? ProductFiscalProfileId { get; set; }
}
