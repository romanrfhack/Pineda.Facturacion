using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.UseCases.SatProductServices;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.UnitTests;

public sealed class SatProductServiceSearchServiceTests
{
    [Fact]
    public async Task ExecuteAsync_PrioritizesExactCodeMatches()
    {
        var service = new SearchSatProductServicesService(new FakeRepository(
        [
            Entry("40161513", "Filtro de aceite"),
            Entry("40161505", "Filtro de aire"),
            Entry("14016151", "Coincidencia irrelevante")
        ]));

        var result = await service.ExecuteAsync("40161513");

        Assert.Equal("40161513", result.Items[0].Code);
        Assert.Equal("exactCode", result.Items[0].MatchKind);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsPrefixCodeMatchesBeforeTextMatches()
    {
        var service = new SearchSatProductServicesService(new FakeRepository(
        [
            Entry("40161513", "Filtro de aceite"),
            Entry("40161505", "Filtro de aire"),
            Entry("25174000", "Sistema de enfriamiento del motor")
        ]));

        var result = await service.ExecuteAsync("4016");

        Assert.Collection(result.Items,
            first =>
            {
                Assert.Equal("40161505", first.Code);
                Assert.Equal("prefixCode", first.MatchKind);
            },
            second =>
            {
                Assert.Equal("40161513", second.Code);
                Assert.Equal("prefixCode", second.MatchKind);
            });
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsNormalizedTextMatches()
    {
        var service = new SearchSatProductServicesService(new FakeRepository(
        [
            Entry("25174000", "Sistema de enfriamiento del motor"),
            Entry("40161513", "Filtro de aceite")
        ]));

        var result = await service.ExecuteAsync("enfriamiento motor");

        Assert.Single(result.Items);
        Assert.Equal("25174000", result.Items[0].Code);
        Assert.Equal("text", result.Items[0].MatchKind);
    }

    [Fact]
    public async Task ExecuteAsync_RespectsTakeLimit()
    {
        var service = new SearchSatProductServicesService(new FakeRepository(
        [
            Entry("40161513", "Filtro de aceite"),
            Entry("40161505", "Filtro de aire"),
            Entry("25174000", "Sistema de enfriamiento del motor")
        ]));

        var result = await service.ExecuteAsync("filtro", take: 1);

        Assert.Single(result.Items);
    }

    [Fact]
    public async Task ExecuteAsync_OrdersExactThenPrefixThenText()
    {
        var service = new SearchSatProductServicesService(new FakeRepository(
        [
            Entry("40161513", "Filtro de aceite"),
            Entry("40161505", "Filtro de aire"),
            Entry("4016", "Codigo corto de prueba"),
            Entry("25174000", "Sistema 4016 de enfriamiento")
        ]));

        var result = await service.ExecuteAsync("4016", take: 10);

        Assert.Collection(result.Items,
            first =>
            {
                Assert.Equal("4016", first.Code);
                Assert.Equal("exactCode", first.MatchKind);
            },
            second => Assert.Equal("40161505", second.Code),
            third => Assert.Equal("40161513", third.Code),
            fourth =>
            {
                Assert.Equal("25174000", fourth.Code);
                Assert.Equal("text", fourth.MatchKind);
            });
    }

    private static SatProductServiceCatalogEntry Entry(string code, string description)
    {
        return new SatProductServiceCatalogEntry
        {
            Code = code,
            Description = description,
            NormalizedDescription = SearchSatProductServicesService.NormalizeSearchText(description),
            KeywordsNormalized = SearchSatProductServicesService.BuildKeywordsNormalized(description),
            IsActive = true,
            SourceVersion = "test"
        };
    }

    private sealed class FakeRepository : ISatProductServiceCatalogRepository
    {
        private readonly IReadOnlyList<SatProductServiceCatalogEntry> _entries;

        public FakeRepository(IReadOnlyList<SatProductServiceCatalogEntry> entries)
        {
            _entries = entries;
        }

        public Task<IReadOnlyList<SatProductServiceCatalogEntry>> SearchAsync(string normalizedQuery, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_entries);
        }
    }
}
