using Microsoft.AspNetCore.Identity;
using Pineda.Facturacion.Application.Abstractions.Security;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.Security;

public sealed class PasswordHasherService : IPasswordHasher
{
    private readonly PasswordHasher<AppUser> _passwordHasher = new();

    public string HashPassword(AppUser user, string password)
    {
        return _passwordHasher.HashPassword(user, password);
    }

    public bool VerifyPassword(AppUser user, string password)
    {
        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
