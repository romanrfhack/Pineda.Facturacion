namespace Pineda.Facturacion.Application.UseCases.Auth;

public static class LoginHardeningPolicy
{
    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    public const int UsernameIpThrottleMaxFailedAttempts = 5;
    public const int IpThrottleMaxFailedAttempts = 20;
    public static readonly TimeSpan ThrottleWindow = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan ThrottleDuration = TimeSpan.FromMinutes(15);

    public static int GetRemainingAttempts(int failedAttemptCount)
    {
        return Math.Max(0, MaxFailedAttempts - failedAttemptCount);
    }
}
