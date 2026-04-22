namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public enum PrepareFiscalDocumentExistingProductFiscalProfileStatus
{
    None = 0,
    Active = 1,
    Inactive = 2
}

public sealed class PrepareFiscalDocumentMissingProductFiscalProfile
{
    public long? BillingDocumentItemId { get; init; }

    public int? LineNumber { get; init; }

    public string? InternalCode { get; init; }

    public string Description { get; init; } = string.Empty;

    public PrepareFiscalDocumentExistingProductFiscalProfileStatus ExistingProfileStatus { get; init; }

    public long? ExistingProductFiscalProfileId { get; init; }

    public bool CanUseExplicitGeneric { get; init; } = true;

    public PrepareFiscalDocumentMissingProductFiscalProfilePrefill Prefill { get; init; } = new();

    public IReadOnlyList<PrepareFiscalDocumentMissingProductFiscalProfileSuggestion> Suggestions { get; init; } = [];
}

public sealed class PrepareFiscalDocumentMissingProductFiscalProfilePrefill
{
    public string SatProductServiceCode { get; init; } = string.Empty;

    public string SatUnitCode { get; init; } = string.Empty;

    public string TaxObjectCode { get; init; } = string.Empty;

    public decimal VatRate { get; init; }

    public string? DefaultUnitText { get; init; }

    public bool IsActive { get; init; } = true;

    public bool RequiresExplicitProductServiceConfirmation { get; init; }
}

public sealed class PrepareFiscalDocumentMissingProductFiscalProfileSuggestion
{
    public string SatProductServiceCode { get; init; } = string.Empty;

    public string? SatProductServiceDescription { get; init; }

    public string SatUnitCode { get; init; } = string.Empty;

    public string? SatUnitDescription { get; init; }

    public string TaxObjectCode { get; init; } = string.Empty;

    public decimal VatRate { get; init; }

    public string? DefaultUnitText { get; init; }

    public decimal Score { get; init; }

    public decimal Confidence { get; init; }

    public string Source { get; init; } = string.Empty;

    public string MatchKind { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;

    public bool IsActive { get; init; }

    public bool RequiresExplicitConfirmation { get; init; }
}
