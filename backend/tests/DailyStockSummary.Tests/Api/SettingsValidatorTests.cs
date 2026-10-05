using DailyStockSummary.Api.Settings;

namespace DailyStockSummary.Tests.Api;

public class SettingsValidatorTests
{
    [Theory]
    [InlineData("*")] // would let any website call the API
    [InlineData("https://app.example.com/")] // copy-pasted with a trailing slash: the browser's Origin never has one
    [InlineData("app.example.com")] // forgot the scheme
    [InlineData("ftp://app.example.com")]
    [InlineData("https://app.example.com/dashboard")] // an origin has no path
    public void A_malformed_cors_origin_is_rejected_because_it_would_silently_never_match(string origin)
    {
        var result = new CorsSettingsValidator().Validate(null, new CorsSettings { AllowedOrigins = [origin] });

        Assert.True(result.Failed);
        Assert.Contains("Cors:AllowedOrigins", string.Join(" ", result.Failures!), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("http://localhost:5173")]
    [InlineData("https://app.example.com")]
    [InlineData("https://app.example.com:8443")]
    public void Real_world_origins_are_accepted(string origin)
    {
        Assert.True(new CorsSettingsValidator().Validate(null, new CorsSettings { AllowedOrigins = [origin] }).Succeeded);
    }

    [Fact]
    public void No_allowed_origins_is_valid_for_a_same_origin_deployment()
    {
        Assert.True(new CorsSettingsValidator().Validate(null, new CorsSettings()).Succeeded);
    }

    [Fact]
    public void A_non_positive_rate_limit_window_is_rejected()
    {
        var result = new RateLimitingSettingsValidator().Validate(null, new RateLimitingSettings { Window = TimeSpan.Zero });

        Assert.True(result.Failed);
        Assert.Contains("RateLimiting:Window", string.Join(" ", result.Failures!), StringComparison.Ordinal);
    }
}
