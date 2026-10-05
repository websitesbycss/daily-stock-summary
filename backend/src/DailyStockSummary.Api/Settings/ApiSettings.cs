using Microsoft.Extensions.Options;

namespace DailyStockSummary.Api.Settings;

public sealed class CorsSettings
{
    public const string SectionName = "Cors";

    public string[] AllowedOrigins { get; set; } = [];
}

public sealed class RateLimitingSettings
{
    public const string SectionName = "RateLimiting";

    public int PermitLimit { get; set; } = 60;

    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);
}

/// <summary>An origin is scheme://host[:port]. A wildcard or a path is a misconfiguration that would silently not match, or match too much.</summary>
public sealed class CorsSettingsValidator : IValidateOptions<CorsSettings>
{
    public ValidateOptionsResult Validate(string? name, CorsSettings options)
    {
        var failures = options.AllowedOrigins
            .Where(origin => !IsPlainOrigin(origin))
            .Select(origin => $"Cors:AllowedOrigins entry '{origin}' must be an absolute http(s) origin such as https://app.example.com (no wildcard, no path).")
            .ToList();

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsPlainOrigin(string origin) =>
        Uri.TryCreate(origin, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https"
        && uri.AbsolutePath == "/"
        && string.IsNullOrEmpty(uri.Query)
        && string.IsNullOrEmpty(uri.Fragment)
        && !origin.EndsWith('/');
}

public sealed class RateLimitingSettingsValidator : IValidateOptions<RateLimitingSettings>
{
    public ValidateOptionsResult Validate(string? name, RateLimitingSettings options)
    {
        var failures = new List<string>();

        if (options.PermitLimit <= 0)
        {
            failures.Add("RateLimiting:PermitLimit must be greater than zero.");
        }

        if (options.Window <= TimeSpan.Zero)
        {
            failures.Add("RateLimiting:Window must be greater than zero.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
