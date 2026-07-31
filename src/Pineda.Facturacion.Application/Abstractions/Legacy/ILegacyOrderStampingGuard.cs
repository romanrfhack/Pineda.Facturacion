using Pineda.Facturacion.Application.Models.Legacy;

namespace Pineda.Facturacion.Application.Abstractions.Legacy;

public interface ILegacyOrderStampingGuard
{
    Task<LegacyOrderStampingValidationResult> ValidateAsync(
        long billingDocumentId,
        CancellationToken cancellationToken = default);
}
