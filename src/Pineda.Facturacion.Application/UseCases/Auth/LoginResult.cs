namespace Pineda.Facturacion.Application.UseCases.Auth;

public sealed class LoginResult
{
    public LoginOutcome Outcome { get; init; }

    public bool IsSuccess { get; init; }

    public string? ErrorMessage { get; init; }

    public long? UserId { get; init; }

    public string? Username { get; init; }

    public string? DisplayName { get; init; }

    public IReadOnlyList<string> Roles { get; init; } = [];

    public string? Token { get; init; }

    public DateTime? ExpiresAtUtc { get; init; }
}
