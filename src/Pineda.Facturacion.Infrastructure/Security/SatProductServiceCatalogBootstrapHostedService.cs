using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Infrastructure.BillingWrite.Persistence;
using Pineda.Facturacion.Infrastructure.SatCatalogs;

namespace Pineda.Facturacion.Infrastructure.Security;

public sealed class SatProductServiceCatalogBootstrapHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly SatProductServiceCatalogSeedSource _seedSource;
    private readonly ILogger<SatProductServiceCatalogBootstrapHostedService> _logger;

    public SatProductServiceCatalogBootstrapHostedService(
        IServiceProvider serviceProvider,
        SatProductServiceCatalogSeedSource seedSource,
        ILogger<SatProductServiceCatalogBootstrapHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _seedSource = seedSource;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var seedDocument = _seedSource.GetDocument();
            if (seedDocument.Items.Count == 0 || string.IsNullOrWhiteSpace(seedDocument.Version))
            {
                _logger.LogWarning("SAT product/service catalog seed source is empty. Bootstrap skipped.");
                return;
            }

            await using var scope = _serviceProvider.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<BillingDbContext>();

            var currentCount = await dbContext.SatProductServiceCatalogEntries.CountAsync(cancellationToken);
            var currentVersionMatches = currentCount == seedDocument.Items.Count
                && await dbContext.SatProductServiceCatalogEntries.AllAsync(x => x.SourceVersion == seedDocument.Version, cancellationToken);

            if (currentVersionMatches)
            {
                return;
            }

            var now = DateTime.UtcNow;
            var existingByCode = await dbContext.SatProductServiceCatalogEntries
                .ToDictionaryAsync(x => x.Code, StringComparer.Ordinal, cancellationToken);

            foreach (var item in seedDocument.Items)
            {
                if (!existingByCode.TryGetValue(item.Code, out var current))
                {
                    await dbContext.SatProductServiceCatalogEntries.AddAsync(new SatProductServiceCatalogEntry
                    {
                        Code = item.Code,
                        Description = item.Description.Trim(),
                        NormalizedDescription = item.NormalizedDescription,
                        KeywordsNormalized = item.KeywordsNormalized,
                        IsActive = item.IsActive,
                        SourceVersion = seedDocument.Version.Trim(),
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now
                    }, cancellationToken);
                    continue;
                }

                current.Description = item.Description.Trim();
                current.NormalizedDescription = item.NormalizedDescription;
                current.KeywordsNormalized = item.KeywordsNormalized;
                current.IsActive = item.IsActive;
                current.SourceVersion = seedDocument.Version.Trim();
                current.UpdatedAtUtc = now;
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "SAT product/service catalog bootstrap completed. Version {Version}. Loaded {Count} entries.",
                seedDocument.Version,
                seedDocument.Items.Count);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "SAT product/service catalog bootstrap skipped due to an unexpected error.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
