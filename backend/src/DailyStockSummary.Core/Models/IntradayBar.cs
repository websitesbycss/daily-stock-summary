namespace DailyStockSummary.Core.Models;

/// <summary>One complete intraday bar. Bars Yahoo reports without a low or high are never represented.</summary>
public sealed record IntradayBar(DateTimeOffset Timestamp, decimal Low, decimal High, long Volume);
