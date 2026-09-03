using System.Collections.Concurrent;

namespace MagmaEdit.AiBridge;

public sealed class AiBridgeRateLimiter
{
    private readonly int _limitPerMinute;
    private readonly ConcurrentDictionary<string, Window> _windows = new(StringComparer.Ordinal);

    public AiBridgeRateLimiter(AiBridgeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _limitPerMinute = options.RateLimitPerMinute;
    }

    public bool TryConsume(string userId, DateTimeOffset nowUtc, out TimeSpan retryAfter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        DateTimeOffset windowStart = new(
            nowUtc.Year,
            nowUtc.Month,
            nowUtc.Day,
            nowUtc.Hour,
            nowUtc.Minute,
            0,
            TimeSpan.Zero);

        Window window = _windows.GetOrAdd(userId, static _ => new Window());
        lock (window)
        {
            if (window.StartUtc != windowStart)
            {
                window.StartUtc = windowStart;
                window.Count = 0;
            }

            if (window.Count >= _limitPerMinute)
            {
                retryAfter = windowStart.AddMinutes(1) - nowUtc;
                return false;
            }

            window.Count++;
            retryAfter = TimeSpan.Zero;
            return true;
        }
    }

    private sealed class Window
    {
        public DateTimeOffset StartUtc { get; set; }

        public int Count { get; set; }
    }
}
