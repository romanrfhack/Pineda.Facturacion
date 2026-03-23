using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Infrastructure.BillingWrite.Persistence;
using Pineda.Facturacion.Infrastructure.Options;

namespace Pineda.Facturacion.Infrastructure.Security;

public sealed class StandardVat16BackfillHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IOptions<BootstrapSeedOptions> _bootstrapOptions;
    private readonly ILogger<StandardVat16BackfillHostedService> _logger;

    public StandardVat16BackfillHostedService(
        IServiceProvider serviceProvider,
        IHostEnvironment hostEnvironment,
        IOptions<BootstrapSeedOptions> bootstrapOptions,
        ILogger<StandardVat16BackfillHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _hostEnvironment = hostEnvironment;
        _bootstrapOptions = bootstrapOptions;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_bootstrapOptions.Value.ApplyStandardVat16BackfillOnStartup || !IsNonProductionBootstrapEnvironment())
        {
            return;
        }

        await using var scope = _serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BillingDbContext>();

        var salesOrders = await dbContext.SalesOrders
            .Include(x => x.Items)
            .Where(x => x.Items.Any(i => i.TaxRate == 0m))
            .ToListAsync(cancellationToken);

        var billingDocuments = await dbContext.BillingDocuments
            .Include(x => x.Items)
            .Where(x => x.Items.Any(i => i.TaxRate == 0m)
                && !dbContext.FiscalDocuments.Any(f => f.BillingDocumentId == x.Id))
            .ToListAsync(cancellationToken);

        foreach (var salesOrder in salesOrders)
        {
            StandardVat16Calculator.ApplyStandardVat(salesOrder);
        }

        foreach (var billingDocument in billingDocuments)
        {
            StandardVat16Calculator.ApplyStandardVat(billingDocument);
            billingDocument.UpdatedAtUtc = DateTime.UtcNow;
        }

        if (salesOrders.Count == 0 && billingDocuments.Count == 0)
        {
            _logger.LogInformation("Standard VAT 16% backfill found no records to normalize in environment {EnvironmentName}.", _hostEnvironment.EnvironmentName);
            return;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Applied Standard VAT 16% backfill in environment {EnvironmentName}. Updated {SalesOrderCount} sales orders and {BillingDocumentCount} billing documents.",
            _hostEnvironment.EnvironmentName,
            salesOrders.Count,
            billingDocuments.Count);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private bool IsNonProductionBootstrapEnvironment()
    {
        return _hostEnvironment.IsDevelopment()
            || _hostEnvironment.IsEnvironment("Local")
            || _hostEnvironment.IsEnvironment("Testing")
            || _hostEnvironment.IsEnvironment("Sandbox");
    }
}
