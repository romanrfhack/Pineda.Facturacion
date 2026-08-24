using Pineda.Facturacion.Application.Models.Legacy;
using Pineda.Facturacion.Infrastructure.Hashing;

namespace Pineda.Facturacion.UnitTests;

public class LegacyPaymentHashCompatibilityTests
{
    [Fact]
    public void GenerateHash_Does_Not_Include_Advisory_Legacy_Payment_Evidence()
    {
        var generator = new Sha256ContentHashGenerator();
        var order = new LegacyOrderReadModel
        {
            LegacyOrderId = "1185568",
            LegacyOrderNumber = "A208345",
            CustomerLegacyId = "1",
            CustomerName = "MOSTRADOR",
            PaymentCondition = "O",
            CurrencyCode = "MXN",
            Total = 100m
        };

        var before = generator.GenerateHash(order);
        order.LegacyPaymentCode = "40";
        order.LegacyPaymentDescription = "Transferencia";
        var after = generator.GenerateHash(order);

        Assert.Equal(before, after);
    }
}
