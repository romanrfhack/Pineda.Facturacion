namespace Pineda.Facturacion.Application.Abstractions.Security;

public interface ILoginAttemptThrottleService
{
    LoginAttemptThrottleStatus Evaluate(string normalizedUsername, string clientIpAddress, DateTime now);

    LoginAttemptThrottleStatus RegisterFailure(string normalizedUsername, string clientIpAddress, DateTime now);

    bool ResetUsernameIpThrottle(string normalizedUsername, string clientIpAddress);
}

public sealed class LoginAttemptThrottleStatus
{
    public bool IsThrottled { get; init; }

    public LoginAttemptThrottleScope? Scope { get; init; }

    public DateTime? ThrottleEndAtUtc { get; init; }

    public IReadOnlyList<LoginAttemptThrottleScope> ExpiredScopes { get; init; } = [];
}

public enum LoginAttemptThrottleScope
{
    UsernameIp = 1,
    Ip = 2
}
