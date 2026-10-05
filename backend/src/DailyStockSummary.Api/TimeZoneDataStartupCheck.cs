namespace DailyStockSummary.Api;

/// <summary>Looks up an IANA time zone by id. A delegate so tests can simulate a machine without time zone data.</summary>
public delegate TimeZoneInfo TimeZoneResolver(string id);

/// <summary>
/// Exchange days are bucketed in the exchange's own time zone. When the operating system has no time zone data
/// (a minimal container image) the parser falls back to a fixed UTC offset, which is wrong whenever daylight saving
/// changes. Failing at startup is better than quietly returning the wrong day totals.
/// </summary>
public sealed class TimeZoneDataStartupCheck(TimeZoneResolver resolve) : IHostedService
{
    /// <summary>One zone per region the app is likely to serve, each with daylight saving rules or a distinct offset.</summary>
    private static readonly string[] RequiredZones = ["America/New_York", "Europe/London", "Asia/Tokyo"];

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var missing = RequiredZones.Where(zone => !IsAvailable(zone)).ToList();

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Time zone data is missing for: {string.Join(", ", missing)}. Install the tzdata package (or the operating system's time zone database) so exchange days are bucketed correctly.");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private bool IsAvailable(string zone)
    {
        try
        {
            resolve(zone);
            return true;
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return false;
        }
    }
}
