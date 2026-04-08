using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.Common;

internal static class FiscalDocumentItemCompositionBuilder
{
    public static Dictionary<string, FiscalDocumentItem> BuildPreservedSemanticMap(
        IReadOnlyList<BillingDocumentItem> currentBillingItems,
        IReadOnlyList<FiscalDocumentItem> currentFiscalItems)
    {
        var preservedByKey = new Dictionary<string, FiscalDocumentItem>(StringComparer.Ordinal);
        var orderedBillingItems = currentBillingItems.OrderBy(x => x.LineNumber).ToList();
        var orderedFiscalItems = currentFiscalItems.OrderBy(x => x.LineNumber).ToList();
        var exactByBillingDocumentItemId = orderedFiscalItems
            .Where(x => x.BillingDocumentItemId.HasValue)
            .GroupBy(x => x.BillingDocumentItemId!.Value)
            .ToDictionary(group => group.Key, group => group.First());
        var lineNumberCandidates = orderedFiscalItems
            .GroupBy(x => x.LineNumber)
            .ToDictionary(group => group.Key, group => new Queue<FiscalDocumentItem>(group));
        var usedFiscalItems = new HashSet<FiscalDocumentItem>();

        for (var index = 0; index < orderedBillingItems.Count; index++)
        {
            var billingDocumentItem = orderedBillingItems[index];
            var key = BillingDocumentItemFiscalSemanticKey.Build(billingDocumentItem);
            if (preservedByKey.ContainsKey(key))
            {
                continue;
            }

            if (billingDocumentItem.Id > 0
                && exactByBillingDocumentItemId.TryGetValue(billingDocumentItem.Id, out var exactMatch)
                && usedFiscalItems.Add(exactMatch))
            {
                preservedByKey[key] = exactMatch;
                continue;
            }

            if (lineNumberCandidates.TryGetValue(billingDocumentItem.LineNumber, out var sameLineCandidates))
            {
                while (sameLineCandidates.Count > 0)
                {
                    var sameLineMatch = sameLineCandidates.Dequeue();
                    if (usedFiscalItems.Add(sameLineMatch))
                    {
                        preservedByKey[key] = sameLineMatch;
                        goto NextBillingItem;
                    }
                }
            }

            if (index < orderedFiscalItems.Count && usedFiscalItems.Add(orderedFiscalItems[index]))
            {
                preservedByKey[key] = orderedFiscalItems[index];
            }

        NextBillingItem:
            continue;
        }

        return preservedByKey;
    }

    public static async Task<List<FiscalDocumentItem>> BuildAsync(
        IReadOnlyList<BillingDocumentItem> billingDocumentItems,
        long fiscalDocumentId,
        IProductFiscalProfileRepository productFiscalProfileRepository,
        Dictionary<string, FiscalDocumentItem>? preservedSemanticsByKey,
        DateTime resolutionAsOfUtc,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var fiscalItems = new List<FiscalDocumentItem>(billingDocumentItems.Count);

        foreach (var billingDocumentItem in billingDocumentItems.OrderBy(x => x.LineNumber))
        {
            if (string.IsNullOrWhiteSpace(billingDocumentItem.ProductInternalCode))
            {
                throw new InvalidOperationException(
                    $"Billing document item line '{billingDocumentItem.LineNumber}' does not contain the persisted product internal code required for fiscal resolution.");
            }

            var normalizedInternalCode = FiscalMasterDataNormalization.NormalizeRequiredCode(billingDocumentItem.ProductInternalCode);
            var semanticKey = BillingDocumentItemFiscalSemanticKey.Build(billingDocumentItem);

            if (preservedSemanticsByKey is not null
                && preservedSemanticsByKey.TryGetValue(semanticKey, out var preservedFiscalItem))
            {
                if (billingDocumentItem.TaxRate != preservedFiscalItem.VatRate)
                {
                    throw new InvalidOperationException(
                        $"Billing document item line '{billingDocumentItem.LineNumber}' tax rate '{billingDocumentItem.TaxRate}' does not match preserved fiscal VAT rate '{preservedFiscalItem.VatRate}'.");
                }

                fiscalItems.Add(CreateFiscalItem(
                    fiscalDocumentId,
                    billingDocumentItem,
                    preservedFiscalItem.InternalCode,
                    preservedFiscalItem.SatProductServiceCode,
                    preservedFiscalItem.SatUnitCode,
                    preservedFiscalItem.TaxObjectCode,
                    preservedFiscalItem.VatRate,
                    preservedFiscalItem.UnitText,
                    preservedFiscalItem.CreatedAtUtc,
                    preservedFiscalItem.BillingDocumentItemId));
                continue;
            }

            var productFiscalProfile = await productFiscalProfileRepository.GetEffectiveByInternalCodeAsync(
                normalizedInternalCode,
                resolutionAsOfUtc,
                cancellationToken);

            if (productFiscalProfile is null || !productFiscalProfile.IsActive)
            {
                throw new InvalidOperationException(
                    $"No active product fiscal profile exists for item line '{billingDocumentItem.LineNumber}' and internal code '{normalizedInternalCode}'.");
            }

            if (billingDocumentItem.TaxRate != productFiscalProfile.VatRate)
            {
                throw new InvalidOperationException(
                    $"Billing document item line '{billingDocumentItem.LineNumber}' tax rate '{billingDocumentItem.TaxRate}' does not match product fiscal profile VAT rate '{productFiscalProfile.VatRate}'.");
            }

            fiscalItems.Add(CreateFiscalItem(
                fiscalDocumentId,
                billingDocumentItem,
                productFiscalProfile.InternalCode,
                productFiscalProfile.SatProductServiceCode,
                productFiscalProfile.SatUnitCode,
                productFiscalProfile.TaxObjectCode,
                productFiscalProfile.VatRate,
                productFiscalProfile.DefaultUnitText,
                now,
                null));
        }

        return fiscalItems;
    }

    private static FiscalDocumentItem CreateFiscalItem(
        long fiscalDocumentId,
        BillingDocumentItem billingDocumentItem,
        string internalCode,
        string satProductServiceCode,
        string satUnitCode,
        string taxObjectCode,
        decimal vatRate,
        string? unitText,
        DateTime createdAtUtc,
        long? preservedBillingDocumentItemId)
    {
        return new FiscalDocumentItem
        {
            FiscalDocumentId = fiscalDocumentId,
            BillingDocumentItemId = billingDocumentItem.Id > 0 ? billingDocumentItem.Id : preservedBillingDocumentItemId,
            LineNumber = billingDocumentItem.LineNumber,
            InternalCode = string.IsNullOrWhiteSpace(internalCode)
                ? FiscalMasterDataNormalization.NormalizeRequiredCode(billingDocumentItem.ProductInternalCode!)
                : internalCode,
            Description = billingDocumentItem.Description,
            Quantity = billingDocumentItem.Quantity,
            UnitPrice = billingDocumentItem.UnitPrice,
            DiscountAmount = billingDocumentItem.DiscountAmount,
            Subtotal = billingDocumentItem.LineTotal,
            TaxTotal = billingDocumentItem.TaxAmount,
            Total = billingDocumentItem.LineTotal + billingDocumentItem.TaxAmount,
            SatProductServiceCode = satProductServiceCode,
            SatUnitCode = satUnitCode,
            TaxObjectCode = taxObjectCode,
            VatRate = vatRate,
            UnitText = unitText,
            CreatedAtUtc = createdAtUtc
        };
    }
}
