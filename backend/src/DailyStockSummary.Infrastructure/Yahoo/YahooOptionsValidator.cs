using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace DailyStockSummary.Infrastructure.Yahoo;

/// <summary>Rejects unusable configuration at startup instead of failing on the first request.</summary>
public sealed class YahooOptionsValidator : IValidateOptions<YahooOptions>
{
    // Polly refuses breaker windows shorter than this, but only once the pipeline is first built.
    private static readonly TimeSpan MinimumBreakerWindow = TimeSpan.FromMilliseconds(500);

    // Anything else makes Yahoo answer 400 to every request, which would only be noticed at runtime.
    private static readonly HashSet<string> Intervals =
        ["1m", "2m", "5m", "15m", "30m", "60m", "90m", "1h", "1d", "5d", "1wk", "1mo", "3mo"];

    private static readonly HashSet<string> Ranges =
        ["1d", "5d", "1mo", "3mo", "6mo", "1y", "2y", "5y", "10y", "ytd", "max"];

    public ValidateOptionsResult Validate(string? name, YahooOptions options)
    {
        var failures = new List<string>();
        var resilience = options.Resilience;

        // A base URL without a trailing slash silently loses its last path segment when requests are built.
        if (options.BaseUrl is null
            || !options.BaseUrl.IsAbsoluteUri
            || options.BaseUrl.Scheme is not ("http" or "https")
            || !options.BaseUrl.AbsolutePath.EndsWith('/'))
        {
            failures.Add("Yahoo:BaseUrl must be an absolute http(s) URL ending with '/'.");
        }

        if (!Intervals.Contains(options.Interval ?? string.Empty))
        {
            failures.Add($"Yahoo:Interval must be one of Yahoo's intervals: {string.Join(", ", Intervals)}.");
        }

        if (!Ranges.Contains(options.Range ?? string.Empty))
        {
            failures.Add($"Yahoo:Range must be one of Yahoo's ranges: {string.Join(", ", Ranges)}.");
        }

        if (!ProductInfoHeaderValue.TryParse(options.UserAgent, out _) && !IsValidUserAgent(options.UserAgent))
        {
            failures.Add("Yahoo:UserAgent must be a valid User-Agent header value.");
        }

        RequirePositive(failures, "Yahoo:CacheTtl", options.CacheTtl);
        RequirePositive(failures, "Yahoo:Resilience:RetryBaseDelay", resilience.RetryBaseDelay);
        RequirePositive(failures, "Yahoo:Resilience:AttemptTimeout", resilience.AttemptTimeout);
        RequirePositive(failures, "Yahoo:Resilience:TotalTimeout", resilience.TotalTimeout);

        if (resilience.RetryCount < 0)
        {
            failures.Add("Yahoo:Resilience:RetryCount must not be negative.");
        }

        if (resilience.TotalTimeout < resilience.AttemptTimeout)
        {
            failures.Add("Yahoo:Resilience:TotalTimeout must be at least AttemptTimeout.");
        }

        if (resilience.BreakerMinimumThroughput < 2)
        {
            failures.Add("Yahoo:Resilience:BreakerMinimumThroughput must be at least 2.");
        }

        if (resilience.BreakerFailureRatio is <= 0 or > 1)
        {
            failures.Add("Yahoo:Resilience:BreakerFailureRatio must be greater than 0 and at most 1.");
        }

        if (resilience.BreakerSamplingDuration < MinimumBreakerWindow)
        {
            failures.Add("Yahoo:Resilience:BreakerSamplingDuration must be at least 500 ms.");
        }

        if (resilience.BreakerBreakDuration < MinimumBreakerWindow)
        {
            failures.Add("Yahoo:Resilience:BreakerBreakDuration must be at least 500 ms.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsValidUserAgent(string userAgent) =>
        !string.IsNullOrWhiteSpace(userAgent) && new HttpRequestMessage().Headers.UserAgent.TryParseAdd(userAgent);

    private static void RequirePositive(List<string> failures, string setting, TimeSpan value)
    {
        if (value <= TimeSpan.Zero)
        {
            failures.Add($"{setting} must be greater than zero.");
        }
    }
}
