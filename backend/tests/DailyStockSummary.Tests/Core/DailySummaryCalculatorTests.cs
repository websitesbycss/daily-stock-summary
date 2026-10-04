using DailyStockSummary.Core.Models;
using DailyStockSummary.Core.Services;

namespace DailyStockSummary.Tests.Core;

public class DailySummaryCalculatorTests
{
    private static readonly TimeZoneInfo NewYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
    private static readonly TimeZoneInfo Tokyo = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");
    private static readonly TimeSpan Edt = TimeSpan.FromHours(-4);

    private readonly DailySummaryCalculator _calculator = new();

    private static IntradaySeries Series(TimeZoneInfo zone, params IntradayBar[] bars) =>
        new(Symbol.Parse("TSLA"), zone, bars);

    private static IntradayBar Bar(DateTimeOffset at, decimal low, decimal high, long volume) =>
        new(at, low, high, volume);

    private static DateTimeOffset Edt930(int month, int day, int minuteOffset = 0) =>
        new DateTimeOffset(2026, month, day, 9, 30, 0, Edt).AddMinutes(minuteOffset);

    [Fact]
    public void Calculate_returns_empty_for_no_bars()
    {
        Assert.Empty(_calculator.Calculate(Series(NewYork)));
    }

    [Fact]
    public void Calculate_averages_lows_and_highs_and_sums_volume_per_day()
    {
        var series = Series(
            NewYork,
            // Sep 8: lows 30.7/3 = 10.2333.., highs 37.6/3 = 12.5333..; volume 4.5B exceeds int.MaxValue
            Bar(Edt930(9, 8, 0), 10.1m, 12.0m, 1_000_000_000),
            Bar(Edt930(9, 8, 15), 10.2m, 12.5m, 1_500_000_000),
            Bar(Edt930(9, 8, 30), 10.4m, 13.1m, 2_000_000_000),
            // Sep 9: lows (20+21)/2 = 20.5, highs (22+23)/2 = 22.5
            Bar(Edt930(9, 9, 0), 20.0m, 22.0m, 5),
            Bar(Edt930(9, 9, 15), 21.0m, 23.0m, 7));

        var result = _calculator.Calculate(series);

        Assert.Equal(
            [
                new DailySummary(new DateOnly(2026, 9, 8), 10.2333m, 12.5333m, 4_500_000_000),
                new DailySummary(new DateOnly(2026, 9, 9), 20.5m, 22.5m, 12),
            ],
            result);
    }

    [Fact]
    public void Calculate_rounds_halves_away_from_zero_at_four_decimals()
    {
        // Mean of 1.0000 and 1.0001 is exactly 1.00005; banker's rounding would give 1.0000.
        var series = Series(
            NewYork,
            Bar(Edt930(9, 8, 0), 1.0000m, 2.0000m, 1),
            Bar(Edt930(9, 8, 15), 1.0001m, 2.0001m, 1));

        var day = Assert.Single(_calculator.Calculate(series));

        Assert.Equal(1.0001m, day.LowAverage);
        Assert.Equal(2.0001m, day.HighAverage);
    }

    [Fact]
    public void Calculate_buckets_a_late_evening_bar_into_the_exchange_local_day_not_the_utc_day()
    {
        // 2026-09-09T02:30Z is Sep 8 at 22:30 in New York.
        var series = Series(
            NewYork,
            Bar(new DateTimeOffset(2026, 9, 9, 2, 30, 0, TimeSpan.Zero), 5m, 6m, 10));

        var day = Assert.Single(_calculator.Calculate(series));

        Assert.Equal(new DateOnly(2026, 9, 8), day.Day);
    }

    [Fact]
    public void Calculate_uses_the_offset_in_force_at_that_instant_including_daylight_saving()
    {
        // 2026-09-09T04:30Z is Sep 9 at 00:30 under EDT (UTC-4). Using the zone's standard offset (UTC-5)
        // would wrongly land it on Sep 8 at 23:30. Matters for near-24h markets such as FX and crypto.
        var series = Series(
            NewYork,
            Bar(new DateTimeOffset(2026, 9, 9, 4, 30, 0, TimeSpan.Zero), 5m, 6m, 10));

        var day = Assert.Single(_calculator.Calculate(series));

        Assert.Equal(new DateOnly(2026, 9, 9), day.Day);
    }

    [Fact]
    public void Calculate_buckets_into_the_next_local_day_for_exchanges_ahead_of_utc()
    {
        // 2026-09-08T16:00Z is Sep 9 at 01:00 in Tokyo.
        var series = Series(
            Tokyo,
            Bar(new DateTimeOffset(2026, 9, 8, 16, 0, 0, TimeSpan.Zero), 5m, 6m, 10));

        var day = Assert.Single(_calculator.Calculate(series));

        Assert.Equal(new DateOnly(2026, 9, 9), day.Day);
    }

    [Fact]
    public void Calculate_orders_days_oldest_first_regardless_of_input_order()
    {
        var series = Series(
            NewYork,
            Bar(Edt930(9, 10), 3m, 4m, 1),
            Bar(Edt930(9, 8), 1m, 2m, 1),
            Bar(Edt930(9, 9), 2m, 3m, 1));

        var days = _calculator.Calculate(series).Select(d => d.Day).ToArray();

        Assert.Equal([new DateOnly(2026, 9, 8), new DateOnly(2026, 9, 9), new DateOnly(2026, 9, 10)], days);
    }
}
