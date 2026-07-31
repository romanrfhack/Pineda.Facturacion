using Pineda.Facturacion.Application.Models.Legacy;

namespace Pineda.Facturacion.Application.Abstractions.Legacy;

public interface ILegacyOrderReader
{
    Task<LegacyOrderReadModel?> GetByIdAsync(string legacyOrderId, CancellationToken cancellationToken = default);

    Task<LegacyOrderPageReadModel> SearchAsync(LegacyOrderSearchReadModel search, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> FindCanceledOrderIdsAsync(
        IReadOnlyCollection<string> legacyOrderIds,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<string>>([]);
    }
}
