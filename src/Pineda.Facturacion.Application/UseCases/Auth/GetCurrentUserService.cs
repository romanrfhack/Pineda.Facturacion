using Pineda.Facturacion.Application.Abstractions.Security;

namespace Pineda.Facturacion.Application.UseCases.Auth;

public sealed class GetCurrentUserService
{
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public GetCurrentUserService(ICurrentUserAccessor currentUserAccessor)
    {
        _currentUserAccessor = currentUserAccessor;
    }

    public GetCurrentUserResult Execute()
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        return new GetCurrentUserResult
        {
            IsAuthenticated = currentUser.IsAuthenticated,
            UserId = currentUser.UserId,
            Username = currentUser.Username,
            DisplayName = currentUser.DisplayName,
            Roles = currentUser.Roles.OrderBy(x => x, StringComparer.Ordinal).ToList()
        };
    }
}
