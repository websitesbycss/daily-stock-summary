using DailyStockSummary.Core.Abstractions;
using DailyStockSummary.Core.Models;

namespace DailyStockSummary.Core.Services;

public sealed class DailySummaryCalculator : IDailySummaryCalculator
{
    private const int Precision = 4;

    public IReadOnlyList<DailySummary> Calculate(IntradaySeries series)
    {
        return series.Bars
            .GroupBy(bar => DayOf(bar, series.ExchangeTimeZone))
            .OrderBy(group => group.Key)
            .Select(group => new DailySummary(
                group.Key,
                Round(group.Average(bar => bar.Low)),
                Round(group.Average(bar => bar.High)),
                group.Sum(bar => bar.Volume)))
            .ToList();
    }

    private static DateOnly DayOf(IntradayBar bar, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(bar.Timestamp, zone).DateTime);

    private static decimal Round(decimal value) =>
        Math.Round(value, Precision, MidpointRounding.AwayFromZero);
}
