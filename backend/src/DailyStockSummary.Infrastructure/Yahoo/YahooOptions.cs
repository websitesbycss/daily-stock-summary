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

    /// <summary>How long a symbol's bars are reused before Yahoo is asked again.</summary>
    public TimeSpan CacheTtl { get; set; } = TimeSpan.FromSeconds(60);

    public YahooResilienceOptions Resilience { get; set; } = new();
}

public sealed class YahooResilienceOptions
{
    /// <summary>Extra attempts after the first one, for transient failures only.</summary>
    public int RetryCount { get; set; } = 2;

    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromMilliseconds(500);

    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Upper bound for one logical request including all retries.</summary>
    public TimeSpan TotalTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public int BreakerMinimumThroughput { get; set; } = 10;

    public double BreakerFailureRatio { get; set; } = 0.5;

    public TimeSpan BreakerSamplingDuration { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan BreakerBreakDuration { get; set; } = TimeSpan.FromSeconds(30);
}
