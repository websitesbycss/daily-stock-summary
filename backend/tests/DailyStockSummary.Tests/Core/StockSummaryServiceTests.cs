using DailyStockSummary.Core.Abstractions;
using DailyStockSummary.Core.Exceptions;
using DailyStockSummary.Core.Models;
using DailyStockSummary.Core.Services;

namespace DailyStockSummary.Tests.Core;

public class StockSummaryServiceTests
{
    private static readonly Symbol Tsla = Symbol.Parse("TSLA");
    private static readonly Symbol Aapl = Symbol.Parse("AAPL");
    private static readonly TimeSpan Edt = TimeSpan.FromHours(-4);
    private static readonly TimeZoneInfo NewYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    private static StockSummaryService ServiceWith(IMarketDataProvider provider) =>
        new(provider, new DailySummaryCalculator());

    private static IntradaySeries OneDay(Symbol symbol, decimal low, decimal high, long volume) =>
        new(symbol, NewYork, [new IntradayBar(new DateTimeOffset(2026, 9, 8, 9, 30, 0, Edt), low, high, volume)]);

    [Fact]
    public async Task GetDailySummariesAsync_summarizes_the_series_for_the_requested_symbol_only()
    {
        var provider = new FakeMarketDataProvider(new Dictionary<Symbol, IntradaySeries>
        {
            [Tsla] = OneDay(Tsla, 360m, 370m, 1_000),
            [Aapl] = OneDay(Aapl, 180m, 185m, 2_000),
        });

        var result = await ServiceWith(provider).GetDailySummariesAsync(Aapl, CancellationToken.None);

        Assert.Equal([new DailySummary(new DateOnly(2026, 9, 8), 180m, 185m, 2_000)], result);
    }

    [Fact]
    public async Task GetDailySummariesAsync_stops_when_the_caller_cancels()
    {
        // The provider only finishes when its token fires, so this fails if the caller's token is dropped.
        var provider = new FakeMarketDataProvider(waitForCancellation: true);
        using var cts = new CancellationTokenSource();

        var pending = ServiceWith(provider).GetDailySummariesAsync(Tsla, cts.Token);
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task GetDailySummariesAsync_lets_symbol_not_found_reach_the_caller()
    {
        // The API turns this into a 404, so it must not be swallowed or wrapped on the way up.
        var provider = new FakeMarketDataProvider(new SymbolNotFoundException(Tsla));

        await Assert.ThrowsAsync<SymbolNotFoundException>(
            () => ServiceWith(provider).GetDailySummariesAsync(Tsla, CancellationToken.None));
    }

    [Fact]
    public async Task GetDailySummariesAsync_lets_upstream_failures_reach_the_caller()
    {
        // The API turns this into a 502, so it must not be swallowed or wrapped on the way up.
        var provider = new FakeMarketDataProvider(new UpstreamUnavailableException("down"));

        await Assert.ThrowsAsync<UpstreamUnavailableException>(
            () => ServiceWith(provider).GetDailySummariesAsync(Tsla, CancellationToken.None));
    }

    private sealed class FakeMarketDataProvider : IMarketDataProvider
    {
        private readonly IReadOnlyDictionary<Symbol, IntradaySeries> _seriesBySymbol = new Dictionary<Symbol, IntradaySeries>();
        private readonly Exception? _failure;
        private readonly bool _waitForCancellation;

        public FakeMarketDataProvider(IReadOnlyDictionary<Symbol, IntradaySeries> seriesBySymbol) =>
            _seriesBySymbol = seriesBySymbol;

        public FakeMarketDataProvider(Exception failure) => _failure = failure;

        public FakeMarketDataProvider(bool waitForCancellation) => _waitForCancellation = waitForCancellation;

        public async Task<IntradaySeries> GetIntradayAsync(Symbol symbol, CancellationToken cancellationToken)
        {
            if (_failure is not null)
            {
                throw _failure;
            }

            if (_waitForCancellation)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            return _seriesBySymbol.TryGetValue(symbol, out var series)
                ? series
                : throw new SymbolNotFoundException(symbol);
        }
    }
}
