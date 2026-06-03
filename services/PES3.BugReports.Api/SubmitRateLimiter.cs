using System.Collections.Concurrent;

namespace PES3.BugReports.Api;

public sealed class SubmitRateLimiter
{
    private readonly ConcurrentDictionary<string, Queue<DateTime>> _hits = new();
    private readonly int _maxPerWindow;
    private readonly TimeSpan _window;

    public SubmitRateLimiter(int maxPerWindow = 10, TimeSpan? window = null)
    {
        _maxPerWindow = maxPerWindow;
        _window = window ?? TimeSpan.FromMinutes(1);
    }

    public bool Allow(string key)
    {
        var now = DateTime.UtcNow;
        var q = _hits.GetOrAdd(key, _ => new Queue<DateTime>());
        lock (q)
        {
            while (q.Count > 0 && now - q.Peek() > _window)
                q.Dequeue();
            if (q.Count >= _maxPerWindow)
                return false;
            q.Enqueue(now);
            return true;
        }
    }
}
