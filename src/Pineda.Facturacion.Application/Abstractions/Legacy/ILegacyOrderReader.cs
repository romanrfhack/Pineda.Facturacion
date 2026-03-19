using Pineda.Facturacion.Application.Models.Legacy;

namespace Pineda.Facturacion.Application.Abstractions.Legacy;

public interface ILegacyOrderReader
{
    Task<LegacyOrderReadModel?> GetByIdAsync(string legacyOrderId, CancellationToken cancellationToken = default);
}
