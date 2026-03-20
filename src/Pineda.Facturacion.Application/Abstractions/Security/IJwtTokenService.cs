using Pineda.Facturacion.Application.Security;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.Abstractions.Security;

public interface IJwtTokenService
{
    JwtTokenResult CreateToken(AppUser user, IReadOnlyCollection<string> roles);
}
