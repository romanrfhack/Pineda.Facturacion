using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.ProductFiscalProfiles;

public sealed class ProductFiscalProfileResolutionResult
{
    public ProductFiscalProfileResolutionStatus Status { get; init; }

    public string Source { get; init; } = string.Empty;

    public decimal Confidence { get; init; }

    public string Reason { get; init; } = string.Empty;

    public ProductFiscalProfile? ResolvedProfile { get; init; }

    public bool ShouldPersistEffectiveAssignment { get; init; }

    public IReadOnlyList<ProductFiscalProfileResolutionCandidate> Candidates { get; init; } = [];
}

public sealed class ProductFiscalProfileResolutionCandidate
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
