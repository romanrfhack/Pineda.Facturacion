using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.ProductFiscalProfiles;

public static class ProductFiscalAssignmentConventions
{
    public const string ManualSource = "product_fiscal_profile_manual";
    public const string ImportSource = "product_fiscal_profile_import";
    public const string BackfillSource = "product_fiscal_profile_backfill";
    public const string LegacyPendingReviewSource = "product_fiscal_profile_pending_review";
    public const string BootstrapReviewStatus = "approved";
    public const string PendingReviewStatus = "pending_review";
    public const string LegacyGenericResetReviewReason = "legacy_generic_01010101_reset";
    public const string GenericSatProductServiceCode = "01010101";
    public const decimal ManualConfidence = 1.0000m;
    public const decimal ImportConfidence = 0.9500m;

    public static bool IsPendingReview(ProductFiscalAssignment? assignment)
    {
        return assignment is not null
            && string.Equals(assignment.ReviewStatus, PendingReviewStatus, StringComparison.Ordinal);
    }

    public static bool IsLegacyGenericResetPending(ProductFiscalAssignment? assignment)
    {
        return assignment is not null
            && string.Equals(assignment.ReviewStatus, PendingReviewStatus, StringComparison.Ordinal)
            && string.Equals(assignment.ReviewReason, LegacyGenericResetReviewReason, StringComparison.Ordinal);
    }

    public static bool IsUnresolvedForSatSuggestion(ProductFiscalAssignment? assignment)
    {
        return IsLegacyGenericResetPending(assignment);
    }

    public static bool IsManagedManualOrImportSource(string? source)
    {
        return string.Equals(source, ManualSource, StringComparison.Ordinal)
            || string.Equals(source, ImportSource, StringComparison.Ordinal);
    }
}
