namespace Pineda.Facturacion.Application.UseCases.ProductFiscalProfiles;

public sealed class ProductFiscalProfileResolutionRequest
{
    public string InternalCode { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string? BillingDocumentItemSatProductServiceCode { get; init; }

    public string? BillingDocumentItemSatUnitCode { get; init; }

    public string? BillingDocumentItemTaxObjectCode { get; init; }

    public decimal BillingDocumentItemVatRate { get; init; }
}
