using DailyStockSummary.Core.Abstractions;
using DailyStockSummary.Core.Exceptions;
using DailyStockSummary.Core.Models;
using DailyStockSummary.Core.Services;

namespace DailyStockSummary.Tests.Core;

public class StockSummaryServiceTests
{
    private static readonly Symbol Tsla = Symbol.Parse("TSLA");
    private static readonly TimeSpan Edt = TimeSpan.FromHours(-4);

    private static StockSummaryService ServiceWith(FakeMarketDataProvider provider) =>
        new(provider, new DailySummaryCalculator());

    [Fact]
    public async Task GetDailySummariesAsync_summarizes_the_series_returned_by_the_provider()
    {
        var series = new IntradaySeries(
            Tsla,
            TimeZoneInfo.FindSystemTimeZoneById("America/New_York"),
            [
                new IntradayBar(new DateTimeOffset(2026, 9, 8, 9, 30, 0, Edt), 10m, 12m, 100),
                new IntradayBar(new DateTimeOffset(2026, 9, 8, 9, 45, 0, Edt), 11m, 14m, 50),
            ]);
        var service = ServiceWith(new FakeMarketDataProvider(series));

        var result = await service.GetDailySummariesAsync(Tsla, CancellationToken.None);

        Assert.Equal([new DailySummary(new DateOnly(2026, 9, 8), 10.5m, 13m, 150)], result);
    }

    [Fact]
    public async Task GetDailySummariesAsync_asks_the_provider_for_the_requested_symbol_with_the_callers_token()
    {
        var provider = new FakeMarketDataProvider(new IntradaySeries(Tsla, TimeZoneInfo.Utc, []));
        using var cts = new CancellationTokenSource();

        await ServiceWith(provider).GetDailySummariesAsync(Tsla, cts.Token);

        var call = Assert.Single(provider.Calls);
        Assert.Equal(Tsla, call.Symbol);
        Assert.Equal(cts.Token, call.Token);
    }

    [Fact]
    public async Task GetDailySummariesAsync_lets_symbol_not_found_reach_the_caller()
    {
        var provider = new FakeMarketDataProvider(new SymbolNotFoundException(Tsla));

        await Assert.ThrowsAsync<SymbolNotFoundException>(
            () => ServiceWith(provider).GetDailySummariesAsync(Tsla, CancellationToken.None));
    }

    [Fact]
    public async Task GetDailySummariesAsync_lets_upstream_failures_reach_the_caller()
    {
        var provider = new FakeMarketDataProvider(new UpstreamUnavailableException("down"));

        await Assert.ThrowsAsync<UpstreamUnavailableException>(
            () => ServiceWith(provider).GetDailySummariesAsync(Tsla, CancellationToken.None));
    }

    private sealed class FakeMarketDataProvider : IMarketDataProvider
    {
        private readonly IntradaySeries? _series;
        private readonly Exception? _failure;

        public FakeMarketDataProvider(IntradaySeries series) => _series = series;

        public FakeMarketDataProvider(Exception failure) => _failure = failure;

        public List<(Symbol Symbol, CancellationToken Token)> Calls { get; } = [];

        public Task<IntradaySeries> GetIntradayAsync(Symbol symbol, CancellationToken cancellationToken)
        {
            Calls.Add((symbol, cancellationToken));
            return _failure is null ? Task.FromResult(_series!) : Task.FromException<IntradaySeries>(_failure);
        }
    }
}
