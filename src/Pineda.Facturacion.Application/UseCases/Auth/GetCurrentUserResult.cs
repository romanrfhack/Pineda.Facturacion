namespace Pineda.Facturacion.Application.UseCases.Auth;

public sealed class GetCurrentUserResult
{
    public bool IsAuthenticated { get; init; }

    public long? UserId { get; init; }

    public string? Username { get; init; }

    public string? DisplayName { get; init; }

    public IReadOnlyList<string> Roles { get; init; } = [];
}
