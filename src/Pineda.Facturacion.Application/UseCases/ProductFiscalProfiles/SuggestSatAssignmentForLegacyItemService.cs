using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Application.UseCases.SatClaveUnidad;
using Pineda.Facturacion.Application.UseCases.SatProductServices;

namespace Pineda.Facturacion.Application.UseCases.ProductFiscalProfiles;

public sealed class SuggestSatAssignmentForLegacyItemService
{
    private readonly IProductFiscalProfileRepository _productFiscalProfileRepository;
    private readonly ISatProductServiceCatalogRepository _satProductServiceCatalogRepository;
    private readonly ISatClaveUnidadRepository _satClaveUnidadRepository;
    private readonly SearchSatProductServicesService _searchSatProductServicesService;
    private readonly SearchSatClaveUnidadService _searchSatClaveUnidadService;

    public SuggestSatAssignmentForLegacyItemService(
        IProductFiscalProfileRepository productFiscalProfileRepository,
        ISatProductServiceCatalogRepository satProductServiceCatalogRepository,
        ISatClaveUnidadRepository satClaveUnidadRepository,
        SearchSatProductServicesService searchSatProductServicesService,
        SearchSatClaveUnidadService searchSatClaveUnidadService)
    {
        _productFiscalProfileRepository = productFiscalProfileRepository;
        _satProductServiceCatalogRepository = satProductServiceCatalogRepository;
        _satClaveUnidadRepository = satClaveUnidadRepository;
        _searchSatProductServicesService = searchSatProductServicesService;
        _searchSatClaveUnidadService = searchSatClaveUnidadService;
    }

    public async Task<SuggestSatAssignmentForLegacyItemResult> ExecuteAsync(
        SuggestSatAssignmentForLegacyItemCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.InternalCode))
        {
            return new SuggestSatAssignmentForLegacyItemResult
            {
                Outcome = SuggestSatAssignmentForLegacyItemOutcome.ValidationFailed,
                IsSuccess = false,
                ErrorMessage = "Internal code is required."
            };
        }

        var internalCode = FiscalMasterDataNormalization.NormalizeRequiredCode(command.InternalCode);
        var existingProfile = await _productFiscalProfileRepository.GetByInternalCodeAsync(internalCode, cancellationToken);
        var effectiveAssignment = await _productFiscalProfileRepository.GetEffectiveAssignmentAsync(internalCode, DateTime.UtcNow, cancellationToken);

        var productCandidates = new List<SatAssignmentSuggestionItem>();
        var productCodes = new HashSet<string>(StringComparer.Ordinal);
        var unitCandidates = new List<SatAssignmentSuggestionItem>();
        var unitCodes = new HashSet<string>(StringComparer.Ordinal);

        await TryAddCurrentAssignmentCandidatesAsync(productCandidates, productCodes, unitCandidates, unitCodes, effectiveAssignment, cancellationToken);
        await TryAddCurrentProfileCandidatesAsync(productCandidates, productCodes, unitCandidates, unitCodes, existingProfile, cancellationToken);
        await TryAddBillingDocumentItemCandidatesAsync(productCandidates, productCodes, unitCandidates, unitCodes, command, cancellationToken);
        await TryAddImportedSnapshotCandidatesAsync(productCandidates, productCodes, unitCandidates, unitCodes, command, cancellationToken);
        await TryAddSearchCandidatesAsync(productCandidates, productCodes, unitCandidates, unitCodes, command, cancellationToken);

        return new SuggestSatAssignmentForLegacyItemResult
        {
            Outcome = SuggestSatAssignmentForLegacyItemOutcome.Suggested,
            IsSuccess = true,
            ExistingProductFiscalProfileId = existingProfile?.Id,
            ProductServiceCandidates = productCandidates,
            UnitCandidates = unitCandidates,
            SuggestedProductService = productCandidates.FirstOrDefault(),
            SuggestedUnit = unitCandidates.FirstOrDefault()
        };
    }

    private async Task TryAddCurrentAssignmentCandidatesAsync(
        List<SatAssignmentSuggestionItem> productCandidates,
        HashSet<string> productCodes,
        List<SatAssignmentSuggestionItem> unitCandidates,
        HashSet<string> unitCodes,
        Domain.Entities.ProductFiscalAssignment? effectiveAssignment,
        CancellationToken cancellationToken)
    {
        if (effectiveAssignment is null)
        {
            return;
        }

        var product = await _satProductServiceCatalogRepository.GetByCodeAsync(effectiveAssignment.SatProductServiceCode, cancellationToken);
        var unit = await _satClaveUnidadRepository.GetByCodeAsync(effectiveAssignment.SatUnitCode, cancellationToken);

        AddCandidate(productCandidates, productCodes, new SatAssignmentSuggestionItem
        {
            Code = effectiveAssignment.SatProductServiceCode,
            Description = product?.Description ?? string.Empty,
            DisplayText = BuildDisplayText(effectiveAssignment.SatProductServiceCode, product?.Description),
            MatchKind = "effectiveAssignment",
            Source = effectiveAssignment.Source,
            Confidence = effectiveAssignment.Confidence,
            Score = 1.0000m,
            IsActive = product?.IsActive ?? false,
            Reason = "Asignacion efectiva vigente para el mismo codigo interno.",
            RequiresExplicitConfirmation = false
        });

        AddCandidate(unitCandidates, unitCodes, new SatAssignmentSuggestionItem
        {
            Code = effectiveAssignment.SatUnitCode,
            Description = unit?.Description ?? string.Empty,
            DisplayText = BuildDisplayText(effectiveAssignment.SatUnitCode, unit?.Description),
            MatchKind = "effectiveAssignment",
            Source = effectiveAssignment.Source,
            Confidence = effectiveAssignment.Confidence,
            Score = 1.0000m,
            IsActive = unit?.IsActive ?? false,
            Reason = "Unidad vigente tomada de la asignacion efectiva del mismo codigo interno.",
            RequiresExplicitConfirmation = false
        });
    }

    private async Task TryAddCurrentProfileCandidatesAsync(
        List<SatAssignmentSuggestionItem> productCandidates,
        HashSet<string> productCodes,
        List<SatAssignmentSuggestionItem> unitCandidates,
        HashSet<string> unitCodes,
        Domain.Entities.ProductFiscalProfile? existingProfile,
        CancellationToken cancellationToken)
    {
        if (existingProfile is null)
        {
            return;
        }

        var product = await _satProductServiceCatalogRepository.GetByCodeAsync(existingProfile.SatProductServiceCode, cancellationToken);
        var unit = await _satClaveUnidadRepository.GetByCodeAsync(existingProfile.SatUnitCode, cancellationToken);

        AddCandidate(productCandidates, productCodes, new SatAssignmentSuggestionItem
        {
            Code = existingProfile.SatProductServiceCode,
            Description = product?.Description ?? existingProfile.Description,
            DisplayText = BuildDisplayText(existingProfile.SatProductServiceCode, product?.Description ?? existingProfile.Description),
            MatchKind = "currentProfile",
            Source = "product_fiscal_profile_current",
            Confidence = existingProfile.IsActive ? 0.9200m : 0.8800m,
            Score = existingProfile.IsActive ? 0.9800m : 0.9700m,
            IsActive = product?.IsActive ?? false,
            Reason = existingProfile.IsActive
                ? "Perfil fiscal actual encontrado para el mismo codigo interno."
                : "Perfil fiscal actual encontrado para el mismo codigo interno, pero el maestro esta inactivo.",
            RequiresExplicitConfirmation = false
        });

        AddCandidate(unitCandidates, unitCodes, new SatAssignmentSuggestionItem
        {
            Code = existingProfile.SatUnitCode,
            Description = unit?.Description ?? existingProfile.DefaultUnitText ?? string.Empty,
            DisplayText = BuildDisplayText(existingProfile.SatUnitCode, unit?.Description ?? existingProfile.DefaultUnitText),
            MatchKind = "currentProfile",
            Source = "product_fiscal_profile_current",
            Confidence = existingProfile.IsActive ? 0.9200m : 0.8800m,
            Score = existingProfile.IsActive ? 0.9800m : 0.9700m,
            IsActive = unit?.IsActive ?? false,
            Reason = existingProfile.IsActive
                ? "Unidad del perfil fiscal actual para el mismo codigo interno."
                : "Unidad del perfil fiscal actual para el mismo codigo interno; el maestro esta inactivo.",
            RequiresExplicitConfirmation = false
        });
    }

    private async Task TryAddBillingDocumentItemCandidatesAsync(
        List<SatAssignmentSuggestionItem> productCandidates,
        HashSet<string> productCodes,
        List<SatAssignmentSuggestionItem> unitCandidates,
        HashSet<string> unitCodes,
        SuggestSatAssignmentForLegacyItemCommand command,
        CancellationToken cancellationToken)
    {
        var itemProductCode = NormalizeOptionalCode(command.BillingDocumentItemSatProductServiceCode);
        if (!string.IsNullOrWhiteSpace(itemProductCode))
        {
            var product = await _satProductServiceCatalogRepository.GetByCodeAsync(itemProductCode, cancellationToken);
            if (product is not null)
            {
                AddCandidate(productCandidates, productCodes, new SatAssignmentSuggestionItem
                {
                    Code = product.Code,
                    Description = product.Description,
                    DisplayText = BuildDisplayText(product.Code, product.Description),
                    MatchKind = "billingDocumentItem",
                    Source = "billing_document_item",
                    Confidence = product.IsActive ? 0.9000m : 0.6500m,
                    Score = 0.9600m,
                    IsActive = product.IsActive,
                    Reason = "Hint SAT persistido en billing_document_item.",
                    RequiresExplicitConfirmation = false
                });
            }
        }

        var itemUnitCode = NormalizeOptionalCode(command.BillingDocumentItemSatUnitCode);
        if (!string.IsNullOrWhiteSpace(itemUnitCode))
        {
            var unit = await _satClaveUnidadRepository.GetByCodeAsync(itemUnitCode, cancellationToken);
            if (unit is not null)
            {
                AddCandidate(unitCandidates, unitCodes, new SatAssignmentSuggestionItem
                {
                    Code = unit.Code,
                    Description = unit.Description,
                    DisplayText = BuildDisplayText(unit.Code, unit.Description),
                    MatchKind = "billingDocumentItem",
                    Source = "billing_document_item",
                    Confidence = unit.IsActive ? 0.9000m : 0.6500m,
                    Score = 0.9600m,
                    IsActive = unit.IsActive,
                    Reason = "Unidad sugerida por los hints SAT persistidos en billing_document_item.",
                    RequiresExplicitConfirmation = false
                });
            }
        }
    }

    private async Task TryAddImportedSnapshotCandidatesAsync(
        List<SatAssignmentSuggestionItem> productCandidates,
        HashSet<string> productCodes,
        List<SatAssignmentSuggestionItem> unitCandidates,
        HashSet<string> unitCodes,
        SuggestSatAssignmentForLegacyItemCommand command,
        CancellationToken cancellationToken)
    {
        var importedProductCode = NormalizeOptionalCode(command.ImportedSatProductServiceCode);
        if (!string.IsNullOrWhiteSpace(importedProductCode))
        {
            var product = await _satProductServiceCatalogRepository.GetByCodeAsync(importedProductCode, cancellationToken);
            if (product is not null)
            {
                AddCandidate(productCandidates, productCodes, new SatAssignmentSuggestionItem
                {
                    Code = product.Code,
                    Description = product.Description,
                    DisplayText = BuildDisplayText(product.Code, product.Description),
                    MatchKind = "legacySnapshot",
                    Source = "legacy_snapshot",
                    Confidence = product.IsActive ? 0.8600m : 0.6000m,
                    Score = 0.9200m,
                    IsActive = product.IsActive,
                    Reason = "Clave SAT recuperada desde un snapshot o importacion historica.",
                    RequiresExplicitConfirmation = false
                });
            }
        }

        var importedUnitCode = NormalizeOptionalCode(command.ImportedSatUnitCode);
        if (!string.IsNullOrWhiteSpace(importedUnitCode))
        {
            var unit = await _satClaveUnidadRepository.GetByCodeAsync(importedUnitCode, cancellationToken);
            if (unit is not null)
            {
                AddCandidate(unitCandidates, unitCodes, new SatAssignmentSuggestionItem
                {
                    Code = unit.Code,
                    Description = unit.Description,
                    DisplayText = BuildDisplayText(unit.Code, unit.Description),
                    MatchKind = "legacySnapshot",
                    Source = "legacy_snapshot",
                    Confidence = unit.IsActive ? 0.8600m : 0.6000m,
                    Score = 0.9200m,
                    IsActive = unit.IsActive,
                    Reason = "Unidad recuperada desde un snapshot o importacion historica.",
                    RequiresExplicitConfirmation = false
                });
            }
        }
    }

    private async Task TryAddSearchCandidatesAsync(
        List<SatAssignmentSuggestionItem> productCandidates,
        HashSet<string> productCodes,
        List<SatAssignmentSuggestionItem> unitCandidates,
        HashSet<string> unitCodes,
        SuggestSatAssignmentForLegacyItemCommand command,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(command.Description))
        {
            var productSearch = await _searchSatProductServicesService.ExecutePagedAsync(command.Description, 1, 5, cancellationToken);
            foreach (var item in productSearch.Items)
            {
                AddCandidate(productCandidates, productCodes, new SatAssignmentSuggestionItem
                {
                    Code = item.Code,
                    Description = item.Description,
                    DisplayText = item.DisplayText,
                    MatchKind = item.MatchKind,
                    Source = "catalog_search",
                    Confidence = MapSearchConfidence(item.Score),
                    Score = item.Score,
                    IsActive = true,
                    Reason = BuildCatalogSearchReason(item.MatchKind),
                    RequiresExplicitConfirmation = true
                });
            }
        }

        var unitSearchText = FirstNonEmpty(command.UnitName, command.Description);
        if (!string.IsNullOrWhiteSpace(unitSearchText))
        {
            var unitSearch = await _searchSatClaveUnidadService.ExecuteAsync(unitSearchText, 1, 5, cancellationToken);
            foreach (var item in unitSearch.Items)
            {
                AddCandidate(unitCandidates, unitCodes, new SatAssignmentSuggestionItem
                {
                    Code = item.Code,
                    Description = item.Description,
                    DisplayText = item.DisplayText,
                    MatchKind = item.MatchKind,
                    Source = "catalog_search",
                    Confidence = MapSearchConfidence(item.Score),
                    Score = item.Score,
                    IsActive = true,
                    Reason = BuildCatalogSearchReason(item.MatchKind),
                    RequiresExplicitConfirmation = true
                });
            }
        }
    }

    private static void AddCandidate(
        List<SatAssignmentSuggestionItem> candidates,
        HashSet<string> knownCodes,
        SatAssignmentSuggestionItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Code) || !knownCodes.Add(item.Code))
        {
            return;
        }

        candidates.Add(item);
        candidates.Sort(CompareCandidates);
    }

    private static int CompareCandidates(SatAssignmentSuggestionItem left, SatAssignmentSuggestionItem right)
    {
        var byScore = right.Score.CompareTo(left.Score);
        if (byScore != 0)
        {
            return byScore;
        }

        var byConfidence = right.Confidence.CompareTo(left.Confidence);
        if (byConfidence != 0)
        {
            return byConfidence;
        }

        return string.Compare(left.Code, right.Code, StringComparison.Ordinal);
    }

    private static decimal MapSearchConfidence(decimal score)
    {
        return score switch
        {
            >= 0.9400m => 0.8200m,
            >= 0.8600m => 0.7600m,
            >= 0.7400m => 0.6800m,
            _ => 0.6200m
        };
    }

    private static string? NormalizeOptionalCode(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : FiscalMasterDataNormalization.NormalizeRequiredCode(value);
    }

    private static string BuildDisplayText(string code, string? description)
    {
        return string.IsNullOrWhiteSpace(description) ? code : $"{code} — {description}";
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
    }

    private static string BuildCatalogSearchReason(string? matchKind)
    {
        return matchKind switch
        {
            "exactCode" => "Coincidencia exacta por codigo en el catalogo SAT local.",
            "prefixCode" => "Coincidencia por prefijo de codigo en el catalogo SAT local.",
            _ => "Coincidencia por descripcion o keywords en el catalogo SAT local."
        };
    }
}
