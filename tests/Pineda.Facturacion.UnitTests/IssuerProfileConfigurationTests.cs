using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Infrastructure.BillingWrite.Persistence;
using Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Configurations;

namespace Pineda.Facturacion.UnitTests;

public class IssuerProfileConfigurationTests
{
    [Fact]
    public void BillingModel_DefinesUniqueActiveIssuerSingletonIndex()
    {
        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        using var context = new BillingDbContext(options);
        var entityType = context.Model.FindEntityType(typeof(IssuerProfile));

        Assert.NotNull(entityType);

        var singletonProperty = entityType!.FindProperty(IssuerProfileConfiguration.ActiveSingletonShadowPropertyName);
        Assert.NotNull(singletonProperty);

        var singletonIndex = entityType.GetIndexes()
            .SingleOrDefault(index => index.Properties.Any(property => property.Name == IssuerProfileConfiguration.ActiveSingletonShadowPropertyName));

        Assert.NotNull(singletonIndex);
        Assert.True(singletonIndex!.IsUnique);
    }
}
