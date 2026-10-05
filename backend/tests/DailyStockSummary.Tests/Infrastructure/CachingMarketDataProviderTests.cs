using DailyStockSummary.Core.Abstractions;
using DailyStockSummary.Core.Exceptions;
using DailyStockSummary.Core.Models;
using DailyStockSummary.Infrastructure;
using Microsoft.Extensions.Time.Testing;

namespace DailyStockSummary.Tests.Infrastructure;

public class CachingMarketDataProviderTests
{
    private static readonly Symbol Tsla = Symbol.Parse("TSLA");
    private static readonly Symbol Aapl = Symbol.Parse("AAPL");
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    private readonly FakeTimeProvider _time = new();

    private CachingMarketDataProvider CacheOver(ScriptedProvider upstream) => new(() => upstream, Ttl, _time);

    [Fact]
    public async Task A_repeat_request_within_the_ttl_is_served_without_asking_yahoo_again()
    {
        var upstream = new ScriptedProvider((symbol, _, _) => Task.FromResult(SeriesFor(symbol)));
        var cache = CacheOver(upstream);

        var first = await cache.GetIntradayAsync(Tsla, CancellationToken.None);
        _time.Advance(TimeSpan.FromSeconds(59));
        var second = await cache.GetIntradayAsync(Tsla, CancellationToken.None);

        Assert.Same(first, second);
        Assert.Equal(1, upstream.Calls);
    }

    [Fact]
    public async Task Once_the_ttl_has_passed_yahoo_is_asked_again_so_data_does_not_go_stale()
    {
        var upstream = new ScriptedProvider((symbol, _, _) => Task.FromResult(SeriesFor(symbol)));
        var cache = CacheOver(upstream);

        var first = await cache.GetIntradayAsync(Tsla, CancellationToken.None);
        _time.Advance(Ttl + TimeSpan.FromSeconds(1));
        var second = await cache.GetIntradayAsync(Tsla, CancellationToken.None);

        Assert.NotSame(first, second);
        Assert.Equal(2, upstream.Calls);
    }

    [Fact]
    public async Task Each_symbol_is_cached_separately_and_never_returns_another_symbols_data()
    {
        var upstream = new ScriptedProvider((symbol, _, _) => Task.FromResult(SeriesFor(symbol)));
        var cache = CacheOver(upstream);

        var tsla = await cache.GetIntradayAsync(Tsla, CancellationToken.None);
        var aapl = await cache.GetIntradayAsync(Aapl, CancellationToken.None);
        var tslaAgain = await cache.GetIntradayAsync(Tsla, CancellationToken.None);

        Assert.Equal(Tsla, tsla.Symbol);
        Assert.Equal(Aapl, aapl.Symbol);
        Assert.Same(tsla, tslaAgain);
        Assert.Equal(2, upstream.Calls);
    }

    public static TheoryData<Exception> Failures => new()
    {
        new UpstreamUnavailableException("Yahoo is down"),
        new SymbolNotFoundException(Tsla), // negative answers are not remembered either
        new OperationCanceledException(), // a fetch that was cancelled out from under its callers
    };

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task A_failure_is_never_cached_so_the_very_next_request_tries_yahoo_again(Exception failure)
    {
        var upstream = new ScriptedProvider((symbol, call, _) =>
            call == 1 ? Failing(failure) : Task.FromResult(SeriesFor(symbol)));
        var cache = CacheOver(upstream);

        await Assert.ThrowsAnyAsync<Exception>(() => cache.GetIntradayAsync(Tsla, CancellationToken.None));
        var recovered = await cache.GetIntradayAsync(Tsla, CancellationToken.None); // no time has passed

        Assert.Equal(Tsla, recovered.Symbol);
        Assert.Equal(2, upstream.Calls);
    }

    [Fact]
    public async Task An_empty_answer_is_not_remembered_so_a_yahoo_glitch_does_not_blank_the_symbol_for_a_minute()
    {
        var upstream = new ScriptedProvider((symbol, call, _) =>
            Task.FromResult(call == 1 ? new IntradaySeries(symbol, TimeZoneInfo.Utc, []) : SeriesFor(symbol)));
        var cache = CacheOver(upstream);

        var glitch = await cache.GetIntradayAsync(Tsla, CancellationToken.None);
        var recovered = await cache.GetIntradayAsync(Tsla, CancellationToken.None);

        Assert.Empty(glitch.Bars);
        Assert.Single(recovered.Bars);
        Assert.Equal(2, upstream.Calls);
    }

    [Fact]
    public async Task Twenty_truly_concurrent_requests_for_one_symbol_make_a_single_call_to_yahoo()
    {
        // Starting an upstream call takes a moment (the inner client builds a request, resolves services, ...).
        // Without the cache's lock every thread would find the entry missing during that window.
        var release = new TaskCompletionSource<IntradaySeries>(TaskCreationOptions.RunContinuationsAsynchronously);
        var upstream = new ScriptedProvider((_, _, _) => release.Task, startupDelay: TimeSpan.FromMilliseconds(25));
        var cache = CacheOver(upstream);
        using var go = new ManualResetEventSlim();

        var requests = Enumerable.Range(0, 20)
            .Select(_ => Task.Run(() =>
            {
                go.Wait();
                return cache.GetIntradayAsync(Tsla, CancellationToken.None);
            }))
            .ToArray();
        go.Set();
        await Task.Delay(150); // let every thread reach the cache before the answer arrives
        release.SetResult(SeriesFor(Tsla));
        var results = await Task.WhenAll(requests);

        Assert.Equal(1, upstream.Calls);
        Assert.All(results, series => Assert.Same(results[0], series));
    }

    [Fact]
    public async Task One_caller_giving_up_does_not_cancel_the_shared_request_for_the_others()
    {
        // A browser tab closing mid-request must not fail the other users waiting on the same symbol.
        // The scripted upstream honours the token it is given, as the real HTTP client does.
        var release = new TaskCompletionSource<IntradaySeries>(TaskCreationOptions.RunContinuationsAsynchronously);
        var upstream = new ScriptedProvider((_, _, token) => release.Task.WaitAsync(token));
        var cache = CacheOver(upstream);
        using var impatient = new CancellationTokenSource();

        var leaving = cache.GetIntradayAsync(Tsla, impatient.Token);
        var staying = cache.GetIntradayAsync(Tsla, CancellationToken.None);
        await impatient.CancelAsync();
        release.SetResult(SeriesFor(Tsla));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => leaving);
        Assert.Equal(Tsla, (await staying).Symbol);
        Assert.Equal(1, upstream.Calls);
    }

    [Fact]
    public async Task A_slow_fetch_that_outlives_the_ttl_is_still_shared_instead_of_starting_a_stampede()
    {
        // Yahoo is struggling: the first fetch is still in flight when its TTL window ends.
        var release = new TaskCompletionSource<IntradaySeries>(TaskCreationOptions.RunContinuationsAsynchronously);
        var upstream = new ScriptedProvider((_, _, _) => release.Task);
        var cache = CacheOver(upstream);

        var first = cache.GetIntradayAsync(Tsla, CancellationToken.None);
        _time.Advance(Ttl + TimeSpan.FromSeconds(5));
        var late = cache.GetIntradayAsync(Tsla, CancellationToken.None);
        release.SetResult(SeriesFor(Tsla));

        Assert.Same(await first, await late);
        Assert.Equal(1, upstream.Calls);
    }

    [Fact]
    public async Task Memory_stays_bounded_by_forgetting_the_oldest_symbol_when_too_many_are_requested()
    {
        var upstream = new ScriptedProvider((symbol, _, _) => Task.FromResult(SeriesFor(symbol)));
        var cache = CacheOver(upstream);
        var symbols = Enumerable.Range(0, 501).Select(i => Symbol.Parse($"S{i}")).ToArray();

        foreach (var symbol in symbols)
        {
            await cache.GetIntradayAsync(symbol, CancellationToken.None);
            _time.Advance(TimeSpan.FromMilliseconds(1));
        }

        var callsBefore = upstream.Calls;
        await cache.GetIntradayAsync(symbols[^1], CancellationToken.None); // newest: still remembered
        Assert.Equal(callsBefore, upstream.Calls);
        await cache.GetIntradayAsync(symbols[0], CancellationToken.None); // oldest: was forgotten
        Assert.Equal(callsBefore + 1, upstream.Calls);
    }

    private static IntradaySeries SeriesFor(Symbol symbol) =>
        new(symbol, TimeZoneInfo.Utc, [new IntradayBar(DateTimeOffset.UnixEpoch, 1m, 2m, 3)]);

    private static Task<IntradaySeries> Failing(Exception failure) =>
        failure is OperationCanceledException
            ? Task.FromCanceled<IntradaySeries>(new CancellationToken(canceled: true))
            : Task.FromException<IntradaySeries>(failure);

    /// <summary>A scripted upstream provider that counts how often it is asked (call numbers are 1-based).</summary>
    private sealed class ScriptedProvider(
        Func<Symbol, int, CancellationToken, Task<IntradaySeries>> respond,
        TimeSpan startupDelay = default) : IMarketDataProvider
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public Task<IntradaySeries> GetIntradayAsync(Symbol symbol, CancellationToken cancellationToken)
        {
            var call = Interlocked.Increment(ref _calls);

            if (startupDelay > TimeSpan.Zero)
            {
                Thread.Sleep(startupDelay);
            }

            return respond(symbol, call, cancellationToken);
        }
    }
}
