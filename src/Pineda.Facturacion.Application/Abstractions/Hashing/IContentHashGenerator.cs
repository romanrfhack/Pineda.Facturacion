using Pineda.Facturacion.Application.Models.Legacy;

namespace Pineda.Facturacion.Application.Abstractions.Hashing;

public interface IContentHashGenerator
{
    string GenerateHash(LegacyOrderReadModel legacyOrder);
}
