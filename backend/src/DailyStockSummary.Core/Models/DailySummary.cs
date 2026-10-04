namespace DailyStockSummary.Core.Models;

/// <summary>One trading day: mean of the 15m lows and highs (4 dp) and total volume.</summary>
public sealed record DailySummary(DateOnly Day, decimal LowAverage, decimal HighAverage, long Volume);
