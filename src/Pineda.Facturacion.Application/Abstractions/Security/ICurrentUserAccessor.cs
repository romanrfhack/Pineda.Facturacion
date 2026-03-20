using Pineda.Facturacion.Application.Security;

namespace Pineda.Facturacion.Application.Abstractions.Security;

public interface ICurrentUserAccessor
{
    CurrentUserContext GetCurrentUser();
}
