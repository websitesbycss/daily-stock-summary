using DailyStockSummary.Core.Abstractions;
using DailyStockSummary.Core.Models;

namespace DailyStockSummary.Infrastructure;

/// <summary>
/// Reuses each symbol's bars for a short time so Yahoo is not hit on every request, and shares one in-flight
/// request between callers asking for the same symbol at the same moment.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Must be a singleton: the cached answers and in-flight requests live inside the instance.</item>
/// <item>Only a successful, non-empty answer is remembered. Failures, cancellations and empty answers (a Yahoo
/// glitch looks like "no bars") are retried by the very next request.</item>
/// <item>The shared fetch is not tied to any caller's token; each caller only stops waiting for itself.</item>
/// <item>The inner provider is created per fetch so <see cref="IHttpClientFactory"/> can keep rotating connections.</item>
/// <item>All timing goes through <see cref="TimeProvider"/>, so expiry is testable without sleeping.</item>
/// </list>
/// </remarks>
public sealed class CachingMarketDataProvider(Func<IMarketDataProvider> innerFactory, TimeSpan ttl, TimeProvider time) : IMarketDataProvider
{
    // Bounds memory when many different symbols are requested; each entry is a few dozen KB.
    private const int MaxCachedSymbols = 500;

    private readonly Dictionary<string, Entry> _entries = [];
    private readonly object _gate = new();
    private long _sequence;

    public Task<IntradaySeries> GetIntradayAsync(Symbol symbol, CancellationToken cancellationToken) =>
        GetOrStartFetch(symbol).WaitAsync(cancellationToken);

    private Task<IntradaySeries> GetOrStartFetch(Symbol symbol)
    {
        lock (_gate)
        {
            var now = time.GetUtcNow();

            if (_entries.TryGetValue(symbol.Value, out var existing) && IsReusable(existing, now))
            {
                return existing.Fetch;
            }

            if (!_entries.ContainsKey(symbol.Value) && _entries.Count >= MaxCachedSymbols)
            {
                MakeRoom(now);
            }

            var fetch = StartFetch(symbol);
            _entries[symbol.Value] = new Entry(fetch, now + ttl, ++_sequence);
            return fetch;
        }
    }

    // An in-flight fetch is always shared, even if its TTL window has passed (Yahoo is slow, not a reason for a
    // stampede). A finished one is reused only while fresh and only if it produced actual bars.
    private static bool IsReusable(Entry entry, DateTimeOffset now) =>
        !entry.Fetch.IsCompleted
        || (entry.Fetch.IsCompletedSuccessfully && entry.Fetch.Result.Bars.Count > 0 && entry.ExpiresAt > now);

    private void MakeRoom(DateTimeOffset now)
    {
        foreach (var key in _entries.Where(pair => !IsReusable(pair.Value, now)).Select(pair => pair.Key).ToList())
        {
            _entries.Remove(key);
        }

        if (_entries.Count < MaxCachedSymbols)
        {
            return;
        }

        // Still full of good entries: forget the oldest finished one (an in-flight one only if nothing else is left).
        var oldest = _entries
            .OrderByDescending(pair => pair.Value.Fetch.IsCompleted)
            .ThenBy(pair => pair.Value.Sequence)
            .First();
        _entries.Remove(oldest.Key);
    }

    private Task<IntradaySeries> StartFetch(Symbol symbol)
    {
        try
        {
            return innerFactory().GetIntradayAsync(symbol, CancellationToken.None);
        }
        catch (Exception ex)
        {
            return Task.FromException<IntradaySeries>(ex);
        }
    }

    private sealed record Entry(Task<IntradaySeries> Fetch, DateTimeOffset ExpiresAt, long Sequence);
}
