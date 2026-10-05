namespace DailyStockSummary.Tests.TestSupport;

public static class TestFixtures
{
    /// <summary>
    /// A real Yahoo response for TSLA (interval=15m, range=1mo): 547 bars over 21 trading days,
    /// Sep 3 to Oct 2 2026. It contains the Labor Day gap and a 27th closing bar on the last day.
    /// </summary>
    public const string TeslaOneMonth = "tsla-15m-one-month.json";

    /// <summary>
    /// A real Yahoo response for BTC-USD (interval=15m, range=1mo): a 24-hour market on the UTC calendar.
    /// 2882 bars over 31 days: a one-bar first day (the window starts mid-day) and a 97-bar latest day.
    /// </summary>
    public const string BitcoinOneMonth = "btc-usd-15m-one-month.json";

    public static string Read(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));
}
