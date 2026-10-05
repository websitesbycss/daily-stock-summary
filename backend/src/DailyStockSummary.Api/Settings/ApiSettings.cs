using System.Net;
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

/// <summary>
/// Trust in a reverse proxy's X-Forwarded-* headers. Off by default: anyone can send those headers, so they are
/// honored only when the connection comes from one of the listed networks.
/// </summary>
public sealed class ForwardedHeadersSettings
{
    public const string SectionName = "ForwardedHeaders";

    public bool Enabled { get; set; }

    /// <summary>CIDR networks of the trusted proxies, for example 172.16.0.0/12 for Docker's default bridge range.</summary>
    public string[] KnownNetworks { get; set; } = [];

    /// <summary>How many proxy hops to unwrap. One reads only the entry the nearest trusted proxy appended.</summary>
    public int ForwardLimit { get; set; } = 1;
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

public sealed class ForwardedHeadersSettingsValidator : IValidateOptions<ForwardedHeadersSettings>
{
    public ValidateOptionsResult Validate(string? name, ForwardedHeadersSettings options)
    {
        var failures = options.KnownNetworks
            .Where(network => !IPNetwork.TryParse(network, out _) || !network.Contains('/', StringComparison.Ordinal))
            .Select(network => $"ForwardedHeaders:KnownNetworks entry '{network}' must be a CIDR network such as 10.0.0.0/8 (use /32 for a single address).")
            .ToList();

        if (options.Enabled && options.KnownNetworks.Length == 0)
        {
            failures.Add("ForwardedHeaders:KnownNetworks must list the trusted proxy networks when ForwardedHeaders:Enabled is true; otherwise no header would ever be honored.");
        }

        if (options.ForwardLimit < 1)
        {
            failures.Add("ForwardedHeaders:ForwardLimit must be at least 1.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
