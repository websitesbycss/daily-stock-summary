using System.Net;
using DailyStockSummary.Api;
using DailyStockSummary.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace DailyStockSummary.Tests.Api;

/// <summary>
/// Without time zone data (a minimal container image) the parser silently falls back to a fixed UTC offset, which
/// puts bars on the wrong exchange day across daylight saving changes. The host must refuse to start instead.
/// </summary>
public class TimeZoneDataStartupCheckTests
{
    private static TimeZoneResolver MissingZones(params string[] missing) => id =>
        missing.Contains(id) ? throw new TimeZoneNotFoundException($"no data for {id}") : TimeZoneInfo.FindSystemTimeZoneById(id);

    [Fact]
    public async Task A_machine_with_time_zone_data_starts_normally()
    {
        var check = new TimeZoneDataStartupCheck(TimeZoneInfo.FindSystemTimeZoneById);

        await check.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task A_missing_zone_stops_startup_and_names_the_zone_and_the_fix()
    {
        var check = new TimeZoneDataStartupCheck(MissingZones("Asia/Tokyo"));

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => check.StartAsync(CancellationToken.None));

        Assert.Contains("Asia/Tokyo", failure.Message, StringComparison.Ordinal);
        Assert.Contains("tzdata", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Every_missing_zone_is_reported_not_just_the_first()
    {
        var check = new TimeZoneDataStartupCheck(MissingZones("America/New_York", "Europe/London"));

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => check.StartAsync(CancellationToken.None));

        Assert.Contains("America/New_York", failure.Message, StringComparison.Ordinal);
        Assert.Contains("Europe/London", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Asia/Tokyo", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_api_host_does_not_start_when_time_zone_data_is_missing()
    {
        using var app = new ApiTestApp(
            StubHttpMessageHandler.Responding(_ => new HttpResponseMessage(HttpStatusCode.OK)),
            configureServices: services => services.AddSingleton(MissingZones("America/New_York")));

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => Task.Run(app.CreateClient));

        Assert.Contains("America/New_York", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_api_host_starts_when_time_zone_data_is_present()
    {
        using var app = new ApiTestApp(StubHttpMessageHandler.Responding(_ => new HttpResponseMessage(HttpStatusCode.OK)));
        using var client = app.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
    }
}
