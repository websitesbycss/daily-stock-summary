using DailyStockSummary.Infrastructure.Yahoo;

namespace DailyStockSummary.Tests.Infrastructure;

public class YahooOptionsValidatorTests
{
    private readonly YahooOptionsValidator _validator = new();

    public static TheoryData<string, Action<YahooOptions>> Misconfigurations => new()
    {
        // The classic: behind a proxy path, a missing trailing slash silently drops "yahoo" when URLs are combined.
        { "Yahoo:BaseUrl", o => o.BaseUrl = new Uri("https://proxy.example.com/yahoo") },
        { "Yahoo:BaseUrl", o => o.BaseUrl = new Uri("query1.finance.yahoo.com/", UriKind.RelativeOrAbsolute) },
        { "Yahoo:BaseUrl", o => o.BaseUrl = new Uri("ftp://query1.finance.yahoo.com/") },
        { "Yahoo:BaseUrl", o => o.BaseUrl = null! }, // an empty value in configuration
        { "Yahoo:Interval", o => o.Interval = "" },
        { "Yahoo:Interval", o => o.Interval = "banana" }, // Yahoo would answer 400 to every request
        { "Yahoo:Interval", o => o.Interval = "15" }, // forgot the unit
        { "Yahoo:Range", o => o.Range = "forever" },
        { "Yahoo:Range", o => o.Range = "  " },
        { "Yahoo:UserAgent", o => o.UserAgent = "" },
        { "Yahoo:UserAgent", o => o.UserAgent = "Mozilla/5.0 (Windows NT 10.0" }, // unclosed comment: HttpClient would throw
        { "Yahoo:CacheTtl", o => o.CacheTtl = TimeSpan.Zero },
        { "Yahoo:Resilience:RetryCount", o => o.Resilience.RetryCount = -1 },
        { "Yahoo:Resilience:AttemptTimeout", o => o.Resilience.AttemptTimeout = TimeSpan.Zero },
        { "Yahoo:Resilience:TotalTimeout", o => o.Resilience.TotalTimeout = TimeSpan.FromSeconds(5) }, // shorter than one attempt
        { "Yahoo:Resilience:BreakerMinimumThroughput", o => o.Resilience.BreakerMinimumThroughput = 1 },
        { "Yahoo:Resilience:BreakerFailureRatio", o => o.Resilience.BreakerFailureRatio = 0 },
        { "Yahoo:Resilience:BreakerFailureRatio", o => o.Resilience.BreakerFailureRatio = 1.5 },
        { "Yahoo:Resilience:BreakerSamplingDuration", o => o.Resilience.BreakerSamplingDuration = TimeSpan.FromMilliseconds(100) }, // Polly needs >= 500ms
        { "Yahoo:Resilience:BreakerBreakDuration", o => o.Resilience.BreakerBreakDuration = TimeSpan.Zero },
    };

    [Fact]
    public void The_shipped_defaults_are_valid()
    {
        Assert.True(_validator.Validate(null, new YahooOptions()).Succeeded);
    }

    [Theory]
    [MemberData(nameof(Misconfigurations))]
    public void A_misconfigured_setting_is_rejected_with_a_message_naming_it(string setting, Action<YahooOptions> misconfigure)
    {
        var options = new YahooOptions();
        misconfigure(options);

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, failure => failure.Contains(setting, StringComparison.Ordinal));
    }

    [Fact]
    public void Every_problem_is_reported_at_once_so_operators_fix_them_in_one_pass()
    {
        var options = new YahooOptions { Interval = "", CacheTtl = TimeSpan.Zero };

        var result = _validator.Validate(null, options);

        Assert.Equal(2, result.Failures!.Count());
    }

    [Theory]
    [InlineData("1m")]
    [InlineData("5m")]
    [InlineData("15m")]
    [InlineData("1h")]
    [InlineData("1d")]
    public void Real_yahoo_intervals_are_accepted(string interval)
    {
        Assert.True(_validator.Validate(null, new YahooOptions { Interval = interval }).Succeeded);
    }

    [Theory]
    [InlineData("1d")]
    [InlineData("5d")]
    [InlineData("1mo")]
    [InlineData("3mo")]
    [InlineData("max")]
    public void Real_yahoo_ranges_are_accepted(string range)
    {
        Assert.True(_validator.Validate(null, new YahooOptions { Range = range }).Succeeded);
    }
}
