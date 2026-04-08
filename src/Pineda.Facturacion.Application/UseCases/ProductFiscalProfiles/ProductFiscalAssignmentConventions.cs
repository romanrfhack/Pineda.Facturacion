namespace Pineda.Facturacion.Application.UseCases.ProductFiscalProfiles;

internal static class ProductFiscalAssignmentConventions
{
    public const string ManualSource = "product_fiscal_profile_manual";
    public const string ImportSource = "product_fiscal_profile_import";
    public const string BootstrapReviewStatus = "approved";
    public const decimal ManualConfidence = 1.0000m;
    public const decimal ImportConfidence = 0.9500m;
}
