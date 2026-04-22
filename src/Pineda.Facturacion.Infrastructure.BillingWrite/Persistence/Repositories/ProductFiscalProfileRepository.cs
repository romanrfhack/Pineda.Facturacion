using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

public class ProductFiscalProfileRepository : IProductFiscalProfileRepository
{
    private readonly BillingDbContext _dbContext;

    public ProductFiscalProfileRepository(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<ProductFiscalProfile>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var prefix = $"{query}%";

        return await _dbContext.ProductFiscalProfiles
            .AsNoTracking()
            .Where(x => EF.Functions.Like(x.InternalCode, prefix) || EF.Functions.Like(x.NormalizedDescription, prefix))
            .Take(20)
            .ToListAsync(cancellationToken);
    }

    public Task<ProductFiscalProfile?> GetByInternalCodeAsync(string normalizedInternalCode, CancellationToken cancellationToken = default)
    {
        return _dbContext.ProductFiscalProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.InternalCode == normalizedInternalCode, cancellationToken);
    }

    public async Task<ProductFiscalProfile?> GetEffectiveByInternalCodeAsync(
        string normalizedInternalCode,
        DateTime asOfUtc,
        CancellationToken cancellationToken = default)
    {
        var assignment = await _dbContext.ProductFiscalAssignments
            .AsNoTracking()
            .Where(x => x.InternalCode == normalizedInternalCode
                && x.ValidFromUtc <= asOfUtc
                && (!x.ValidToUtc.HasValue || x.ValidToUtc > asOfUtc))
            .OrderByDescending(x => x.ValidFromUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (assignment is not null)
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

        return await _dbContext.ProductFiscalProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.InternalCode == normalizedInternalCode, cancellationToken);
    }

    public Task<ProductFiscalAssignment?> GetEffectiveAssignmentAsync(
        string normalizedInternalCode,
        DateTime asOfUtc,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.ProductFiscalAssignments
            .AsNoTracking()
            .Where(x => x.InternalCode == normalizedInternalCode
                && x.ValidFromUtc <= asOfUtc
                && (!x.ValidToUtc.HasValue || x.ValidToUtc > asOfUtc))
            .OrderByDescending(x => x.ValidFromUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<ProductFiscalProfile?> GetByIdAsync(long productFiscalProfileId, CancellationToken cancellationToken = default)
    {
        return _dbContext.ProductFiscalProfiles
            .FirstOrDefaultAsync(x => x.Id == productFiscalProfileId, cancellationToken);
    }

    public async Task AddAsync(ProductFiscalProfile productFiscalProfile, CancellationToken cancellationToken = default)
    {
        await _dbContext.ProductFiscalProfiles.AddAsync(productFiscalProfile, cancellationToken);
    }

    public Task UpdateAsync(ProductFiscalProfile productFiscalProfile, CancellationToken cancellationToken = default)
    {
        _dbContext.ProductFiscalProfiles.Update(productFiscalProfile);
        return Task.CompletedTask;
    }

    public async Task EnsureEffectiveAssignmentAsync(
        ProductFiscalProfile productFiscalProfile,
        string source,
        decimal confidence,
        string reviewStatus,
        string? reviewReason,
        DateTime effectiveAtUtc,
        CancellationToken cancellationToken = default)
    {
        var current = await _dbContext.ProductFiscalAssignments
            .Where(x => x.InternalCode == productFiscalProfile.InternalCode && !x.ValidToUtc.HasValue)
            .OrderByDescending(x => x.ValidFromUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var normalizedReviewReason = string.IsNullOrWhiteSpace(reviewReason) ? null : reviewReason.Trim();

        if (current is not null && HasSameFiscalSemantics(current, productFiscalProfile))
        {
            current.Source = source.Trim();
            current.Confidence = confidence;
            current.ReviewStatus = reviewStatus.Trim();
            current.ReviewReason = normalizedReviewReason;
            current.UpdatedAtUtc = now;
            return;
        }

        if (current is not null)
        {
            current.ValidToUtc = effectiveAtUtc;
            current.UpdatedAtUtc = now;
        }

        await _dbContext.ProductFiscalAssignments.AddAsync(new ProductFiscalAssignment
        {
            InternalCode = productFiscalProfile.InternalCode,
            SatProductServiceCode = productFiscalProfile.SatProductServiceCode,
            SatUnitCode = productFiscalProfile.SatUnitCode,
            TaxObjectCode = productFiscalProfile.TaxObjectCode,
            VatRate = productFiscalProfile.VatRate,
            DefaultUnitText = productFiscalProfile.DefaultUnitText,
            Source = source.Trim(),
            Confidence = confidence,
            ReviewStatus = reviewStatus.Trim(),
            ReviewReason = normalizedReviewReason,
            ValidFromUtc = effectiveAtUtc,
            ValidToUtc = null,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        }, cancellationToken);
    }

    private static bool HasSameFiscalSemantics(ProductFiscalAssignment current, ProductFiscalProfile productFiscalProfile)
    {
        return string.Equals(current.SatProductServiceCode, productFiscalProfile.SatProductServiceCode, StringComparison.Ordinal)
            && string.Equals(current.SatUnitCode, productFiscalProfile.SatUnitCode, StringComparison.Ordinal)
            && string.Equals(current.TaxObjectCode, productFiscalProfile.TaxObjectCode, StringComparison.Ordinal)
            && current.VatRate == productFiscalProfile.VatRate
            && string.Equals(current.DefaultUnitText ?? string.Empty, productFiscalProfile.DefaultUnitText ?? string.Empty, StringComparison.Ordinal);
    }
}
