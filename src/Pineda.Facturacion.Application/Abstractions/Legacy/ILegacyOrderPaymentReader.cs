namespace Pineda.Facturacion.Application.Abstractions.Legacy;

public interface ILegacyOrderPaymentReader
{
    Task<LegacyOrderPaymentReadModel?> GetByOrderIdAsync(
        string legacyOrderId,
        CancellationToken cancellationToken = default);
}

public sealed class LegacyOrderPaymentReadModel
{
    public string? LegacyPaymentCode { get; init; }

    public string? LegacyPaymentDescription { get; init; }
}
