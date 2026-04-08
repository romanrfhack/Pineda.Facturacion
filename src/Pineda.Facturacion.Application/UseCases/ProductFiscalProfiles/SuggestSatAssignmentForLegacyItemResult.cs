namespace Pineda.Facturacion.Application.UseCases.ProductFiscalProfiles;

public sealed class SuggestSatAssignmentForLegacyItemResult
{
    public SuggestSatAssignmentForLegacyItemOutcome Outcome { get; init; }

    public bool IsSuccess { get; init; }

    public string? ErrorMessage { get; init; }

    public long? ExistingProductFiscalProfileId { get; init; }

    public IReadOnlyList<SatAssignmentSuggestionItem> ProductServiceCandidates { get; init; } = [];

    public IReadOnlyList<SatAssignmentSuggestionItem> UnitCandidates { get; init; } = [];

    public SatAssignmentSuggestionItem? SuggestedProductService { get; init; }

    public SatAssignmentSuggestionItem? SuggestedUnit { get; init; }
}

public sealed class SatAssignmentSuggestionItem
{
    public string Code { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string DisplayText { get; init; } = string.Empty;

    public string MatchKind { get; init; } = string.Empty;

    public string Source { get; init; } = string.Empty;

    public decimal Confidence { get; init; }

    public decimal Score { get; init; }

    public bool IsActive { get; init; }
}
