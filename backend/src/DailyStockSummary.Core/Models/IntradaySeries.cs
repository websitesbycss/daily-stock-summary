namespace DailyStockSummary.Core.Models;

/// <summary>Intraday bars for one symbol, plus the timezone of the exchange they trade on.</summary>
public sealed record IntradaySeries(Symbol Symbol, TimeZoneInfo ExchangeTimeZone, IReadOnlyList<IntradayBar> Bars);
