using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.Abstractions.Security;

public interface IPasswordHasher
{
    string HashPassword(AppUser user, string password);

    bool VerifyPassword(AppUser user, string password);
}
