namespace Pineda.Facturacion.Application.UseCases.ProductFiscalProfiles;

public class CreateProductFiscalProfileResult
{
    public CreateProductFiscalProfileOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public long? ProductFiscalProfileId { get; set; }
}
