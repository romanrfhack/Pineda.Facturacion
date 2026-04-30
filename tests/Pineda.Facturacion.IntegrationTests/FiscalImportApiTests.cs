using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pineda.Facturacion.Application.Security;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;
using Pineda.Facturacion.Infrastructure.BillingWrite.Persistence;

namespace Pineda.Facturacion.IntegrationTests;

public class FiscalImportApiTests
{
    [Fact]
    public async Task LegacyProductMappingBatches_CanBeListed_BySupervisor()
    {
        await using var factory = new MvpApiFactory();
        await factory.SeedUserAsync("supervisor-legacy", "Secret123!", true, AppRoleNames.FiscalSupervisor);
        await SeedLegacyProductMappingBatchAsync(factory);
        var client = await factory.CreateAuthenticatedClientAsync("supervisor-legacy", "Secret123!");

        var response = await client.GetAsync("/api/fiscal/imports/products/legacy-mappings/batches");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var batches = await response.Content.ReadFromJsonAsync<List<LegacyProductMappingBatchApiResponse>>();
        var batch = Assert.Single(batches!);
        Assert.Equal("legacy.csv", batch.FileName);
        Assert.Equal("Sistema anterior", batch.SourceName);
        Assert.Equal("supervisor-legacy", batch.ImportedByUser);
        Assert.Equal(3, batch.TotalRows);
        Assert.Equal(2, batch.ValidRows);
        Assert.Equal(1, batch.InvalidRows);
        Assert.Equal(1, batch.AmbiguousRows);
        Assert.Equal(0, batch.SkippedRows);
        Assert.Equal("Validated", batch.Status);

        var rawBody = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("sourceChecksum", rawBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ConnectionStrings", rawBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password", rawBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LegacyProductMappingBatches_RejectsFiscalOperator()
    {
        await using var factory = new MvpApiFactory();
        await factory.SeedUserAsync("operator-legacy", "Secret123!", true, AppRoleNames.FiscalOperator);
        var client = await factory.CreateAuthenticatedClientAsync("operator-legacy", "Secret123!");

        var response = await client.GetAsync("/api/fiscal/imports/products/legacy-mappings/batches");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task SeedLegacyProductMappingBatchAsync(MvpApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();

        db.FiscalProductMappingImportBatches.Add(new FiscalProductMappingImportBatch
        {
            FileName = "legacy.csv",
            SourceName = "Sistema anterior",
            SourceChecksum = "sha256:test",
            ImportedAtUtc = DateTime.UtcNow,
            ImportedByUserId = 10,
            ImportedByUsername = "supervisor-legacy",
            TotalRows = 3,
            ValidRows = 2,
            InvalidRows = 1,
            AmbiguousRows = 1,
            SkippedRows = 0,
            Status = ImportBatchStatus.Validated,
            ErrorMessage = null
        });

        await db.SaveChangesAsync();
        Assert.Equal(1, await db.FiscalProductMappingImportBatches.CountAsync());
    }

    private sealed class LegacyProductMappingBatchApiResponse
    {
        public long Id { get; set; }

        public string FileName { get; set; } = string.Empty;

        public string SourceName { get; set; } = string.Empty;

        public DateTime ImportedAtUtc { get; set; }

        public string? ImportedByUser { get; set; }

        public int TotalRows { get; set; }

        public int ValidRows { get; set; }

        public int InvalidRows { get; set; }

        public int AmbiguousRows { get; set; }

        public int SkippedRows { get; set; }

        public string Status { get; set; } = string.Empty;

        public string? ErrorMessage { get; set; }
    }
}
