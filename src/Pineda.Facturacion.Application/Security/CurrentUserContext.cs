namespace Pineda.Facturacion.Application.Security;

public sealed class CurrentUserContext
{
    public bool IsAuthenticated { get; init; }

    public long? UserId { get; init; }

    public string? Username { get; init; }

    public string? DisplayName { get; init; }

    public IReadOnlyCollection<string> Roles { get; init; } = Array.Empty<string>();
}
