using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Pineda.Facturacion.Application.Abstractions.Security;
using Pineda.Facturacion.Application.Security;

namespace Pineda.Facturacion.Infrastructure.Security;

public sealed class CurrentUserAccessor : ICurrentUserAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public CurrentUserContext GetCurrentUser()
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return new CurrentUserContext { IsAuthenticated = false };
        }

        long? userId = null;
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (long.TryParse(userIdValue, out var parsedUserId))
        {
            userId = parsedUserId;
        }

        return new CurrentUserContext
        {
            IsAuthenticated = true,
            UserId = userId,
            Username = principal.FindFirstValue(ClaimTypes.Name),
            DisplayName = principal.FindFirstValue("display_name"),
            Roles = principal.FindAll(ClaimTypes.Role).Select(x => x.Value).Distinct(StringComparer.Ordinal).ToList()
        };
    }
}
