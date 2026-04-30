using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.ProductFiscalProfiles;

public sealed class ProductFiscalProfileResolver
{
    public const string SourceConfirmedProfile = "confirmed_profile";
    public const string SourceExistingProfile = "existing_profile";
    public const string SourceLegacyMapping = "legacy_mapping";
    public const string SourceCurrentAutoDetection = "current_auto_detection";

    private readonly IProductFiscalProfileRepository _productFiscalProfileRepository;
    private readonly ILegacyFiscalProductMappingRepository _legacyFiscalProductMappingRepository;
    private readonly ISatProductServiceCatalogRepository _satProductServiceCatalogRepository;
    private readonly ISatClaveUnidadRepository _satClaveUnidadRepository;
    private readonly SuggestSatAssignmentForLegacyItemService _suggestSatAssignmentForLegacyItemService;

    public ProductFiscalProfileResolver(
        IProductFiscalProfileRepository productFiscalProfileRepository,
        ILegacyFiscalProductMappingRepository legacyFiscalProductMappingRepository,
        ISatProductServiceCatalogRepository satProductServiceCatalogRepository,
        ISatClaveUnidadRepository satClaveUnidadRepository,
        SuggestSatAssignmentForLegacyItemService suggestSatAssignmentForLegacyItemService)
    {
        _productFiscalProfileRepository = productFiscalProfileRepository;
        _legacyFiscalProductMappingRepository = legacyFiscalProductMappingRepository;
        _satProductServiceCatalogRepository = satProductServiceCatalogRepository;
        _satClaveUnidadRepository = satClaveUnidadRepository;
        _suggestSatAssignmentForLegacyItemService = suggestSatAssignmentForLegacyItemService;
    }

    public async Task<ProductFiscalProfileResolutionResult> ResolveAsync(
        ProductFiscalProfileResolutionRequest request,
        DateTime asOfUtc,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.InternalCode))
        {
            return Unresolved("Internal code is required for product fiscal profile resolution.");
        }

        var normalizedInternalCode = FiscalMasterDataNormalization.NormalizeRequiredCode(request.InternalCode);
        var normalizedLegacyInternalKey = FiscalProductTextNormalization.NormalizeOptionalKey(request.InternalCode)
            ?? normalizedInternalCode;
        var normalizedDescription = FiscalProductTextNormalization.NormalizeOptionalText(request.Description);
        var effectiveAssignment = await _productFiscalProfileRepository.GetEffectiveAssignmentAsync(
            normalizedInternalCode,
            asOfUtc,
            cancellationToken);
        var existingProfile = await _productFiscalProfileRepository.GetByInternalCodeAsync(
            normalizedInternalCode,
            cancellationToken);
        var suppressExistingProfile = ProductFiscalAssignmentConventions.IsUnresolvedForSatSuggestion(effectiveAssignment);

        if (effectiveAssignment is not null
            && !suppressExistingProfile)
        {
            return new ProductFiscalProfileResolutionResult
            {
                Status = ProductFiscalProfileResolutionStatus.Resolved,
                Source = IsManualAssignment(effectiveAssignment)
                    ? SourceConfirmedProfile
                    : SourceExistingProfile,
                Confidence = effectiveAssignment.Confidence,
                Reason = IsManualAssignment(effectiveAssignment)
                    ? "Perfil fiscal confirmado previamente por el usuario para el mismo codigo interno."
                    : "Asignacion fiscal efectiva vigente para el mismo codigo interno.",
                ResolvedProfile = BuildProfileFromAssignment(effectiveAssignment)
            };
        }

        if (!suppressExistingProfile)
        {
            var effectiveProfile = await _productFiscalProfileRepository.GetEffectiveByInternalCodeAsync(
                normalizedInternalCode,
                asOfUtc,
                cancellationToken);
            if (effectiveProfile is { IsActive: true })
            {
                return new ProductFiscalProfileResolutionResult
                {
                    Status = ProductFiscalProfileResolutionStatus.Resolved,
                    Source = SourceExistingProfile,
                    Confidence = 0.9200m,
                    Reason = "Perfil fiscal efectivo existente para el mismo codigo interno.",
                    ResolvedProfile = effectiveProfile
                };
            }
        }

        if (!suppressExistingProfile && existingProfile is { IsActive: true })
        {
            return new ProductFiscalProfileResolutionResult
            {
                Status = ProductFiscalProfileResolutionStatus.Resolved,
                Source = SourceExistingProfile,
                Confidence = 0.9200m,
                Reason = "Perfil fiscal activo existente para el mismo codigo interno.",
                ResolvedProfile = existingProfile
            };
        }

        var legacyResult = await TryResolveLegacyMappingAsync(
            normalizedLegacyInternalKey,
            normalizedDescription,
            request,
            existingProfile,
            cancellationToken);
        if (legacyResult is not null)
        {
            return legacyResult;
        }

        var currentSuggestions = await _suggestSatAssignmentForLegacyItemService.ExecuteAsync(
            new SuggestSatAssignmentForLegacyItemCommand
            {
                InternalCode = normalizedInternalCode,
                Description = request.Description,
                BillingDocumentItemSatProductServiceCode = request.BillingDocumentItemSatProductServiceCode,
                BillingDocumentItemSatUnitCode = request.BillingDocumentItemSatUnitCode,
                SuppressHistoricalCandidates = ProductFiscalAssignmentConventions.IsUnresolvedForSatSuggestion(effectiveAssignment)
            },
            cancellationToken);

        var candidates = currentSuggestions.ProductServiceCandidates
            .Where(x => !string.Equals(x.Code, ProductFiscalAssignmentConventions.GenericSatProductServiceCode, StringComparison.Ordinal))
            .Select(x => MapCurrentSuggestion(x, currentSuggestions, request, existingProfile))
            .ToList();

        if (candidates.Count > 0)
        {
            return new ProductFiscalProfileResolutionResult
            {
                Status = ProductFiscalProfileResolutionStatus.Suggested,
                Source = SourceCurrentAutoDetection,
                Confidence = candidates[0].Confidence,
                Reason = "Sugerencias calculadas con la logica automatica actual del sistema.",
                Candidates = candidates
            };
        }

        return Unresolved("No se encontro perfil fiscal confirmado, mapping legado ni sugerencia SAT confiable.");
    }

    private async Task<ProductFiscalProfileResolutionResult?> TryResolveLegacyMappingAsync(
        string normalizedInternalCode,
        string? normalizedDescription,
        ProductFiscalProfileResolutionRequest request,
        ProductFiscalProfile? existingProfile,
        CancellationToken cancellationToken)
    {
        var exactCandidates = await _legacyFiscalProductMappingRepository.FindActiveExactCandidatesAsync(
            normalizedInternalCode,
            normalizedDescription,
            cancellationToken);

        var bothCandidates = exactCandidates
            .Where(x => MatchesInternalCode(x, normalizedInternalCode) && MatchesDescription(x, normalizedDescription))
            .ToArray();
        var bothResult = await BuildLegacyResultAsync(
            bothCandidates,
            "exactInternalAndDescription",
            "Coincidencia exacta por codigo interno y descripcion.",
            0.9800m,
            0.9700m,
            request,
            existingProfile,
            cancellationToken);
        if (bothResult is not null)
        {
            return bothResult;
        }

        var internalCandidates = exactCandidates
            .Where(x => MatchesInternalCode(x, normalizedInternalCode))
            .ToArray();
        var internalResult = await BuildLegacyResultAsync(
            internalCandidates,
            "exactInternalCode",
            "Coincidencia exacta por codigo interno.",
            0.9400m,
            0.9300m,
            request,
            existingProfile,
            cancellationToken);
        if (internalResult is not null)
        {
            return internalResult;
        }

        var descriptionCandidates = exactCandidates
            .Where(x => MatchesDescription(x, normalizedDescription))
            .ToArray();
        var descriptionResult = await BuildLegacyResultAsync(
            descriptionCandidates,
            "exactDescription",
            $"Coincidencia exacta por descripcion: {normalizedDescription}.",
            0.9000m,
            0.9000m,
            request,
            existingProfile,
            cancellationToken);
        if (descriptionResult is not null)
        {
            return descriptionResult;
        }

        if (!string.IsNullOrWhiteSpace(normalizedDescription))
        {
            var fuzzyCandidates = await _legacyFiscalProductMappingRepository.FindActiveDescriptionCandidatesAsync(
                normalizedDescription,
                8,
                cancellationToken);
            var ranked = fuzzyCandidates
                .Select(x => (Mapping: x, Score: CalculateTokenScore(normalizedDescription, x.DescriptionNormalized)))
                .Where(x => x.Score >= 0.6000m)
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Mapping.SatProductServiceCode, StringComparer.Ordinal)
                .Take(5)
                .Select(x => BuildLegacyCandidate(
                    x.Mapping,
                    "fuzzyDescription",
                    "Coincidencia aproximada por descripcion importada; requiere validacion del usuario.",
                    x.Score,
                    Math.Min(0.7600m, x.Score),
                    request,
                    existingProfile,
                    null,
                    null,
                    requiresExplicitConfirmation: true))
                .ToList();

            if (ranked.Count > 0)
            {
                return new ProductFiscalProfileResolutionResult
                {
                    Status = ProductFiscalProfileResolutionStatus.Suggested,
                    Source = SourceLegacyMapping,
                    Confidence = ranked[0].Confidence,
                    Reason = "Se encontraron coincidencias aproximadas en el historial fiscal importado.",
                    Candidates = ranked
                };
            }
        }

        return null;
    }

    private async Task<ProductFiscalProfileResolutionResult?> BuildLegacyResultAsync(
        IReadOnlyList<LegacyFiscalProductMapping> mappings,
        string matchKind,
        string reason,
        decimal score,
        decimal confidence,
        ProductFiscalProfileResolutionRequest request,
        ProductFiscalProfile? existingProfile,
        CancellationToken cancellationToken)
    {
        if (mappings.Count == 0)
        {
            return null;
        }

        var distinctProductCodes = mappings
            .Select(x => x.SatProductServiceCode)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var isAmbiguous = distinctProductCodes.Length > 1
            || mappings.Any(x => matchKind.Contains("Description", StringComparison.Ordinal) && x.IsAmbiguousByDescription)
            || mappings.Any(x => matchKind.Contains("Internal", StringComparison.Ordinal) && x.IsAmbiguousByInternalCode);

        var candidates = new List<ProductFiscalProfileResolutionCandidate>();
        foreach (var mapping in mappings
                     .GroupBy(x => $"{x.SatProductServiceCode}|{x.SatUnitCode}", StringComparer.Ordinal)
                     .Select(group => group.First())
                     .OrderBy(x => x.SatProductServiceCode, StringComparer.Ordinal))
        {
            var product = await _satProductServiceCatalogRepository.GetByCodeAsync(mapping.SatProductServiceCode, cancellationToken);
            Pineda.Facturacion.Domain.Entities.SatClaveUnidad? unit = null;
            if (!string.IsNullOrWhiteSpace(mapping.SatUnitCode))
            {
                unit = await _satClaveUnidadRepository.GetByCodeAsync(mapping.SatUnitCode, cancellationToken);
            }

            candidates.Add(BuildLegacyCandidate(
                mapping,
                isAmbiguous ? $"{matchKind}Ambiguous" : matchKind,
                isAmbiguous
                    ? "El historial fiscal importado contiene mas de una clave SAT para esta coincidencia; requiere validacion."
                    : reason,
                score,
                isAmbiguous ? 0.5000m : confidence,
                request,
                existingProfile,
                product,
                unit,
                requiresExplicitConfirmation: isAmbiguous
                    || string.Equals(mapping.SatProductServiceCode, ProductFiscalAssignmentConventions.GenericSatProductServiceCode, StringComparison.Ordinal)));
        }

        if (isAmbiguous)
        {
            return new ProductFiscalProfileResolutionResult
            {
                Status = ProductFiscalProfileResolutionStatus.Ambiguous,
                Source = SourceLegacyMapping,
                Confidence = 0.5000m,
                Reason = "Mapping legado ambiguo por descripcion o codigo interno.",
                Candidates = candidates
            };
        }

        var first = candidates[0];
        if (first.RequiresExplicitConfirmation)
        {
            return new ProductFiscalProfileResolutionResult
            {
                Status = ProductFiscalProfileResolutionStatus.Suggested,
                Source = SourceLegacyMapping,
                Confidence = first.Confidence,
                Reason = "El mapping legado encontro una clave que requiere confirmacion explicita.",
                Candidates = candidates
            };
        }

        var mappingForProfile = mappings.First(x =>
            string.Equals(x.SatProductServiceCode, first.SatProductServiceCode, StringComparison.Ordinal)
            && string.Equals(x.SatUnitCode ?? string.Empty, first.SatUnitCode, StringComparison.Ordinal));

        return new ProductFiscalProfileResolutionResult
        {
            Status = ProductFiscalProfileResolutionStatus.Resolved,
            Source = SourceLegacyMapping,
            Confidence = first.Confidence,
            Reason = first.Reason,
            ResolvedProfile = BuildProfileFromLegacyMapping(mappingForProfile, request, existingProfile, first.SatUnitDescription),
            ShouldPersistEffectiveAssignment = true,
            Candidates = candidates
        };
    }

    private ProductFiscalProfileResolutionCandidate BuildLegacyCandidate(
        LegacyFiscalProductMapping mapping,
        string matchKind,
        string reason,
        decimal score,
        decimal confidence,
        ProductFiscalProfileResolutionRequest request,
        ProductFiscalProfile? existingProfile,
        SatProductServiceCatalogEntry? product,
        Pineda.Facturacion.Domain.Entities.SatClaveUnidad? unit,
        bool requiresExplicitConfirmation)
    {
        var satUnitCode = ResolveSatUnitCode(mapping.SatUnitCode, request, existingProfile);
        var unitDescription = unit?.Description;
        var taxObjectCode = ResolveTaxObjectCode(request, existingProfile);
        var vatRate = ResolveVatRate(request, existingProfile);

        return new ProductFiscalProfileResolutionCandidate
        {
            SatProductServiceCode = mapping.SatProductServiceCode,
            SatProductServiceDescription = product?.Description,
            SatUnitCode = satUnitCode,
            SatUnitDescription = unitDescription,
            TaxObjectCode = taxObjectCode,
            VatRate = vatRate,
            DefaultUnitText = ResolveDefaultUnitText(existingProfile?.DefaultUnitText, unitDescription, satUnitCode),
            Score = score,
            Confidence = confidence,
            Source = SourceLegacyMapping,
            MatchKind = matchKind,
            Reason = reason,
            IsActive = product?.IsActive ?? true,
            RequiresExplicitConfirmation = requiresExplicitConfirmation
        };
    }

    private static ProductFiscalProfileResolutionCandidate MapCurrentSuggestion(
        SatAssignmentSuggestionItem item,
        SuggestSatAssignmentForLegacyItemResult currentSuggestions,
        ProductFiscalProfileResolutionRequest request,
        ProductFiscalProfile? existingProfile)
    {
        var unit = currentSuggestions.UnitCandidates.FirstOrDefault(x =>
                string.Equals(x.Source, item.Source, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(x.Code))
            ?? currentSuggestions.SuggestedUnit;
        var unitCode = unit?.Code
            ?? NormalizeOptionalCode(request.BillingDocumentItemSatUnitCode)
            ?? existingProfile?.SatUnitCode
            ?? "H87";
        var unitDescription = unit?.Description;

        return new ProductFiscalProfileResolutionCandidate
        {
            SatProductServiceCode = item.Code,
            SatProductServiceDescription = item.Description,
            SatUnitCode = unitCode,
            SatUnitDescription = unitDescription,
            TaxObjectCode = ResolveTaxObjectCode(request, existingProfile),
            VatRate = ResolveVatRate(request, existingProfile),
            DefaultUnitText = ResolveDefaultUnitText(existingProfile?.DefaultUnitText, unitDescription, unitCode),
            Score = item.Score,
            Confidence = item.Confidence,
            Source = item.Source,
            MatchKind = item.MatchKind,
            Reason = item.Reason,
            IsActive = item.IsActive && (unit?.IsActive ?? true),
            RequiresExplicitConfirmation = item.RequiresExplicitConfirmation
        };
    }

    private static ProductFiscalProfile BuildProfileFromAssignment(ProductFiscalAssignment assignment)
    {
        return new ProductFiscalProfile
        {
            InternalCode = assignment.InternalCode,
            SatProductServiceCode = assignment.SatProductServiceCode,
            SatUnitCode = assignment.SatUnitCode,
            TaxObjectCode = assignment.TaxObjectCode,
            VatRate = assignment.VatRate,
            DefaultUnitText = assignment.DefaultUnitText,
            IsActive = true,
            CreatedAtUtc = assignment.CreatedAtUtc,
            UpdatedAtUtc = assignment.UpdatedAtUtc
        };
    }

    private static ProductFiscalProfile BuildProfileFromLegacyMapping(
        LegacyFiscalProductMapping mapping,
        ProductFiscalProfileResolutionRequest request,
        ProductFiscalProfile? existingProfile,
        string? unitDescription)
    {
        var normalizedInternalCode = FiscalMasterDataNormalization.NormalizeRequiredCode(request.InternalCode);
        var description = FiscalMasterDataNormalization.NormalizeOptionalText(request.Description)
            ?? existingProfile?.Description
            ?? mapping.DescriptionRaw
            ?? normalizedInternalCode;
        var satUnitCode = ResolveSatUnitCode(mapping.SatUnitCode, request, existingProfile);

        return new ProductFiscalProfile
        {
            InternalCode = normalizedInternalCode,
            Description = description,
            NormalizedDescription = FiscalMasterDataNormalization.NormalizeSearchableText(description),
            SatProductServiceCode = mapping.SatProductServiceCode,
            SatUnitCode = satUnitCode,
            TaxObjectCode = ResolveTaxObjectCode(request, existingProfile),
            VatRate = ResolveVatRate(request, existingProfile),
            DefaultUnitText = ResolveDefaultUnitText(existingProfile?.DefaultUnitText, unitDescription, satUnitCode),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    private static string ResolveSatUnitCode(
        string? mappingUnitCode,
        ProductFiscalProfileResolutionRequest request,
        ProductFiscalProfile? existingProfile)
    {
        return NormalizeOptionalCode(mappingUnitCode)
            ?? NormalizeOptionalCode(request.BillingDocumentItemSatUnitCode)
            ?? existingProfile?.SatUnitCode
            ?? "H87";
    }

    private static string ResolveTaxObjectCode(ProductFiscalProfileResolutionRequest request, ProductFiscalProfile? existingProfile)
    {
        return NormalizeOptionalCode(request.BillingDocumentItemTaxObjectCode)
            ?? existingProfile?.TaxObjectCode
            ?? "02";
    }

    private static decimal ResolveVatRate(ProductFiscalProfileResolutionRequest request, ProductFiscalProfile? existingProfile)
    {
        return request.BillingDocumentItemVatRate > 0
            ? request.BillingDocumentItemVatRate
            : existingProfile?.VatRate ?? 0.160000m;
    }

    private static string ResolveDefaultUnitText(string? currentValue, string? unitDescription, string fallbackUnitCode)
    {
        return FiscalMasterDataNormalization.NormalizeOptionalText(currentValue)
            ?? FiscalMasterDataNormalization.NormalizeOptionalText(unitDescription)
            ?? (string.Equals(fallbackUnitCode, "H87", StringComparison.Ordinal) ? "PIEZA" : null)
            ?? string.Empty;
    }

    private static bool MatchesInternalCode(LegacyFiscalProductMapping mapping, string normalizedInternalCode)
    {
        return string.Equals(mapping.InternalCatalogNormalized, normalizedInternalCode, StringComparison.Ordinal)
            || string.Equals(mapping.SkuCodeNormalized, normalizedInternalCode, StringComparison.Ordinal)
            || string.Equals(mapping.EanCodeNormalized, normalizedInternalCode, StringComparison.Ordinal);
    }

    private static bool MatchesDescription(LegacyFiscalProductMapping mapping, string? normalizedDescription)
    {
        return !string.IsNullOrWhiteSpace(normalizedDescription)
            && string.Equals(mapping.DescriptionNormalized, normalizedDescription, StringComparison.Ordinal);
    }

    private static decimal CalculateTokenScore(string left, string right)
    {
        var leftTokens = left.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        var rightTokens = right.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        if (leftTokens.Count == 0 || rightTokens.Count == 0)
        {
            return 0m;
        }

        var intersection = leftTokens.Count(rightTokens.Contains);
        var union = leftTokens.Concat(rightTokens).Distinct(StringComparer.Ordinal).Count();
        return decimal.Round((decimal)intersection / union, 4, MidpointRounding.AwayFromZero);
    }

    private static string? NormalizeOptionalCode(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : FiscalMasterDataNormalization.NormalizeRequiredCode(value);
    }

    private static bool IsManualAssignment(ProductFiscalAssignment assignment)
    {
        return string.Equals(assignment.Source, ProductFiscalAssignmentConventions.ManualSource, StringComparison.Ordinal);
    }

    private static ProductFiscalProfileResolutionResult Unresolved(string reason)
    {
        return new ProductFiscalProfileResolutionResult
        {
            Status = ProductFiscalProfileResolutionStatus.Unresolved,
            Source = "manual",
            Confidence = 0m,
            Reason = reason
        };
    }
}
