using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Pineda.Facturacion.Application.UseCases.Auth;

namespace Pineda.Facturacion.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/auth")
            .WithTags("Auth");

        group.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .WithName("Login")
            .WithSummary("Authenticate with local username and password")
            .Produces<LoginResponse>(StatusCodes.Status200OK)
            .Produces<LoginResponse>(StatusCodes.Status400BadRequest)
            .Produces<LoginResponse>(StatusCodes.Status401Unauthorized);

        group.MapGet("/me", GetMeAsync)
            .RequireAuthorization()
            .WithName("GetCurrentUser")
            .WithSummary("Get the current authenticated user")
            .Produces<CurrentUserResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        return endpoints;
    }

    private static async Task<Results<Ok<LoginResponse>, BadRequest<LoginResponse>, UnauthorizedHttpResult>> LoginAsync(
        LoginRequest request,
        LoginService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(new LoginCommand
        {
            Username = request.Username,
            Password = request.Password
        }, cancellationToken);

        var response = new LoginResponse
        {
            Outcome = result.Outcome.ToString(),
            IsSuccess = result.IsSuccess,
            ErrorMessage = result.ErrorMessage,
            Token = result.Token,
            ExpiresAtUtc = result.ExpiresAtUtc,
            User = result.UserId is null
                ? null
                : new CurrentUserResponse
                {
                    Id = result.UserId,
                    Username = result.Username,
                    DisplayName = result.DisplayName,
                    Roles = result.Roles,
                    IsAuthenticated = result.IsSuccess
                }
        };

        return result.Outcome switch
        {
            LoginOutcome.Authenticated => TypedResults.Ok(response),
            LoginOutcome.ValidationFailed => TypedResults.BadRequest(response),
            _ => TypedResults.Unauthorized()
        };
    }

    [Authorize]
    private static Ok<CurrentUserResponse> GetMeAsync(GetCurrentUserService service)
    {
        var currentUser = service.Execute();
        return TypedResults.Ok(new CurrentUserResponse
        {
            Id = currentUser.UserId,
            Username = currentUser.Username,
            DisplayName = currentUser.DisplayName,
            Roles = currentUser.Roles,
            IsAuthenticated = currentUser.IsAuthenticated
        });
    }
}

public sealed class LoginRequest
{
    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}

public sealed class LoginResponse
{
    public string Outcome { get; init; } = string.Empty;

    public bool IsSuccess { get; init; }

    public string? ErrorMessage { get; init; }

    public string? Token { get; init; }

    public DateTime? ExpiresAtUtc { get; init; }

    public CurrentUserResponse? User { get; init; }
}

public sealed class CurrentUserResponse
{
    public long? Id { get; init; }

    public string? Username { get; init; }

    public string? DisplayName { get; init; }

    public IReadOnlyList<string> Roles { get; init; } = [];

    public bool IsAuthenticated { get; init; }
}
