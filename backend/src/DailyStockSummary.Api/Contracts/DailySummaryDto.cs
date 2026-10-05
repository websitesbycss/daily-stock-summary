using DailyStockSummary.Core.Models;

namespace DailyStockSummary.Api.Contracts;

/// <summary>The public wire contract for one trading day. Property order is the JSON order.</summary>
public sealed record DailySummaryDto(DateOnly Day, decimal LowAverage, decimal HighAverage, long Volume)
{
    public static DailySummaryDto From(DailySummary summary) =>
        new(summary.Day, summary.LowAverage, summary.HighAverage, summary.Volume);
}
