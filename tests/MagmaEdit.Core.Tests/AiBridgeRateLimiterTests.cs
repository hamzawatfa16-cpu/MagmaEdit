using MagmaEdit.AiBridge;

namespace MagmaEdit.Core.Tests;

public sealed class AiBridgeRateLimiterTests
{
    [Fact]
    public void AllowsRequestsUpToConfiguredLimit()
    {
        AiBridgeRateLimiter limiter = new(new AiBridgeOptions { RateLimitPerMinute = 2 });
        DateTimeOffset now = new(2026, 9, 3, 18, 0, 0, TimeSpan.Zero);

        Assert.True(limiter.TryConsume("user-a", now, out _));
        Assert.True(limiter.TryConsume("user-a", now.AddSeconds(1), out _));
        Assert.False(limiter.TryConsume("user-a", now.AddSeconds(2), out TimeSpan retryAfter));
        Assert.True(retryAfter > TimeSpan.Zero);
    }

    [Fact]
    public void LimitsAreIndependentPerUser()
    {
        AiBridgeRateLimiter limiter = new(new AiBridgeOptions { RateLimitPerMinute = 1 });
        DateTimeOffset now = new(2026, 9, 3, 18, 0, 0, TimeSpan.Zero);

        Assert.True(limiter.TryConsume("user-a", now, out _));
        Assert.False(limiter.TryConsume("user-a", now.AddSeconds(1), out _));
        Assert.True(limiter.TryConsume("user-b", now.AddSeconds(1), out _));
    }

    [Fact]
    public void WindowResetsOnNextMinute()
    {
        AiBridgeRateLimiter limiter = new(new AiBridgeOptions { RateLimitPerMinute = 1 });
        DateTimeOffset now = new(2026, 9, 3, 18, 0, 59, TimeSpan.Zero);

        Assert.True(limiter.TryConsume("user-a", now, out _));
        Assert.False(limiter.TryConsume("user-a", now.AddMilliseconds(100), out _));
        Assert.True(limiter.TryConsume("user-a", new DateTimeOffset(2026, 9, 3, 18, 1, 0, TimeSpan.Zero), out _));
    }
}
