namespace DailyStockSummary.Infrastructure.Yahoo;

public sealed class YahooOptions
{
    public const string SectionName = "Yahoo";

    public Uri BaseUrl { get; set; } = new("https://query1.finance.yahoo.com/");

    /// <summary>Yahoo rejects requests that do not look like they come from a browser.</summary>
    public string UserAgent { get; set; } =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36";

    public string Interval { get; set; } = "15m";

    public string Range { get; set; } = "1mo";
}
