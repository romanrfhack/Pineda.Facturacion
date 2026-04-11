using Pineda.Facturacion.Application.Abstractions.Security;
using Pineda.Facturacion.Application.UseCases.Auth;

namespace Pineda.Facturacion.Infrastructure.Security;

public sealed class InMemoryLoginAttemptThrottleService : ILoginAttemptThrottleService
{
    private readonly object _sync = new();
    private readonly Dictionary<string, ThrottleBucketState> _usernameIpBuckets = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ThrottleBucketState> _ipBuckets = new(StringComparer.OrdinalIgnoreCase);

    public LoginAttemptThrottleStatus Evaluate(string normalizedUsername, string clientIpAddress, DateTime now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedUsername);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientIpAddress);

        lock (_sync)
        {
            var expiredScopes = new List<LoginAttemptThrottleScope>(2);
            var usernameIpKey = BuildUsernameIpKey(normalizedUsername, clientIpAddress);

            var usernameIpStatus = EvaluateBucket(_usernameIpBuckets, usernameIpKey, now, LoginAttemptThrottleScope.UsernameIp, expiredScopes);
            if (usernameIpStatus.IsThrottled)
            {
                return usernameIpStatus;
            }

            var ipStatus = EvaluateBucket(_ipBuckets, clientIpAddress, now, LoginAttemptThrottleScope.Ip, expiredScopes);
            if (ipStatus.IsThrottled)
            {
                return ipStatus;
            }

            return new LoginAttemptThrottleStatus
            {
                ExpiredScopes = expiredScopes
            };
        }
    }

    public LoginAttemptThrottleStatus RegisterFailure(string normalizedUsername, string clientIpAddress, DateTime now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedUsername);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientIpAddress);

        lock (_sync)
        {
            var expiredScopes = new List<LoginAttemptThrottleScope>(2);
            var usernameIpKey = BuildUsernameIpKey(normalizedUsername, clientIpAddress);

            var usernameIpStatus = RegisterFailure(
                _usernameIpBuckets,
                usernameIpKey,
                now,
                LoginHardeningPolicy.UsernameIpThrottleMaxFailedAttempts,
                LoginHardeningPolicy.ThrottleWindow,
                LoginHardeningPolicy.ThrottleDuration,
                LoginAttemptThrottleScope.UsernameIp,
                expiredScopes);

            var ipStatus = RegisterFailure(
                _ipBuckets,
                clientIpAddress,
                now,
                LoginHardeningPolicy.IpThrottleMaxFailedAttempts,
                LoginHardeningPolicy.ThrottleWindow,
                LoginHardeningPolicy.ThrottleDuration,
                LoginAttemptThrottleScope.Ip,
                expiredScopes);

            if (usernameIpStatus.IsThrottled)
            {
                return usernameIpStatus;
            }

            if (ipStatus.IsThrottled)
            {
                return ipStatus;
            }

            return new LoginAttemptThrottleStatus
            {
                ExpiredScopes = expiredScopes
            };
        }
    }

    public bool ResetUsernameIpThrottle(string normalizedUsername, string clientIpAddress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedUsername);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientIpAddress);

        lock (_sync)
        {
            return _usernameIpBuckets.Remove(BuildUsernameIpKey(normalizedUsername, clientIpAddress));
        }
    }

    private static LoginAttemptThrottleStatus EvaluateBucket(
        Dictionary<string, ThrottleBucketState> buckets,
        string key,
        DateTime now,
        LoginAttemptThrottleScope scope,
        List<LoginAttemptThrottleScope> expiredScopes)
    {
        if (!buckets.TryGetValue(key, out var bucket))
        {
            return new LoginAttemptThrottleStatus
            {
                ExpiredScopes = expiredScopes
            };
        }

        if (bucket.BlockedUntilUtc.HasValue)
        {
            if (bucket.BlockedUntilUtc.Value <= now)
            {
                buckets.Remove(key);
                expiredScopes.Add(scope);
                return new LoginAttemptThrottleStatus
                {
                    ExpiredScopes = expiredScopes
                };
            }

            return new LoginAttemptThrottleStatus
            {
                IsThrottled = true,
                Scope = scope,
                ThrottleEndAtUtc = bucket.BlockedUntilUtc,
                ExpiredScopes = expiredScopes
            };
        }

        if (bucket.WindowStartedAtUtc + LoginHardeningPolicy.ThrottleWindow <= now)
        {
            buckets.Remove(key);
        }

        return new LoginAttemptThrottleStatus
        {
            ExpiredScopes = expiredScopes
        };
    }

    private static LoginAttemptThrottleStatus RegisterFailure(
        Dictionary<string, ThrottleBucketState> buckets,
        string key,
        DateTime now,
        int threshold,
        TimeSpan window,
        TimeSpan duration,
        LoginAttemptThrottleScope scope,
        List<LoginAttemptThrottleScope> expiredScopes)
    {
        if (!buckets.TryGetValue(key, out var bucket))
        {
            bucket = new ThrottleBucketState
            {
                WindowStartedAtUtc = now
            };
            buckets[key] = bucket;
        }
        else if (bucket.BlockedUntilUtc.HasValue && bucket.BlockedUntilUtc.Value <= now)
        {
            expiredScopes.Add(scope);
            bucket.Reset(now);
        }
        else if (bucket.WindowStartedAtUtc + window <= now)
        {
            bucket.Reset(now);
        }

        if (bucket.BlockedUntilUtc.HasValue && bucket.BlockedUntilUtc.Value > now)
        {
            return new LoginAttemptThrottleStatus
            {
                IsThrottled = true,
                Scope = scope,
                ThrottleEndAtUtc = bucket.BlockedUntilUtc,
                ExpiredScopes = expiredScopes
            };
        }

        bucket.FailureCount++;
        if (bucket.FailureCount >= threshold)
        {
            bucket.BlockedUntilUtc = now.Add(duration);
            return new LoginAttemptThrottleStatus
            {
                IsThrottled = true,
                Scope = scope,
                ThrottleEndAtUtc = bucket.BlockedUntilUtc,
                ExpiredScopes = expiredScopes
            };
        }

        return new LoginAttemptThrottleStatus
        {
            ExpiredScopes = expiredScopes
        };
    }

    private static string BuildUsernameIpKey(string normalizedUsername, string clientIpAddress)
    {
        return $"{clientIpAddress}::{normalizedUsername}";
    }

    private sealed class ThrottleBucketState
    {
        public int FailureCount { get; set; }

        public DateTime WindowStartedAtUtc { get; set; }

        public DateTime? BlockedUntilUtc { get; set; }

        public void Reset(DateTime now)
        {
            FailureCount = 0;
            WindowStartedAtUtc = now;
            BlockedUntilUtc = null;
        }
    }
}
