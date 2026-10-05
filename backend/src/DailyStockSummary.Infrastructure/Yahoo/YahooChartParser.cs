using System.Text.Json;
using DailyStockSummary.Core.Exceptions;
using DailyStockSummary.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DailyStockSummary.Infrastructure.Yahoo;

/// <summary>Turns a Yahoo v8 chart payload into an <see cref="IntradaySeries"/> or a typed failure.</summary>
internal static partial class YahooChartParser
{
    private const string NotFoundCode = "Not Found";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IntradaySeries Parse(Symbol symbol, string json, ILogger? logger = null)
    {
        logger ??= NullLogger.Instance;

        var chart = Deserialize(json)?.Chart
            ?? throw new UpstreamUnavailableException("Yahoo returned a response without chart data.");

        if (chart.Error is { } error)
        {
            throw string.Equals(error.Code, NotFoundCode, StringComparison.OrdinalIgnoreCase)
                ? new SymbolNotFoundException(symbol)
                : new UpstreamUnavailableException($"Yahoo reported an error: {error.Code}.");
        }

        if (chart.Result is not [{ } result, ..])
        {
            throw new UpstreamUnavailableException("Yahoo returned a response without a result.");
        }

        var zone = ResolveTimeZone(result.Meta, symbol, logger);

        if (result.Timestamp is not { Count: > 0 } timestamps)
        {
            // Legitimate for an illiquid symbol, but also what a Yahoo glitch looks like.
            LogNoBars(logger, symbol.Value);
            return new IntradaySeries(symbol, zone, []);
        }

        var quote = result.Indicators?.Quote is [var first, ..] ? first : null;

        return new IntradaySeries(symbol, zone, ReadBars(timestamps, quote, symbol, logger));
    }

    private static YahooChartResponse? Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<YahooChartResponse>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new UpstreamUnavailableException("Yahoo returned a response that is not valid JSON.", ex);
        }
    }

    private static List<IntradayBar> ReadBars(IReadOnlyList<long> timestamps, YahooQuote? quote, Symbol symbol, ILogger logger)
    {
        if (quote is not { Low: { } lows, High: { } highs, Volume: { } volumes }
            || lows.Count != timestamps.Count
            || highs.Count != timestamps.Count
            || volumes.Count != timestamps.Count)
        {
            throw new UpstreamUnavailableException("Yahoo returned quote data that does not line up with its timestamps.");
        }

        var bars = new List<IntradayBar>(timestamps.Count);

        for (var i = 0; i < timestamps.Count; i++)
        {
            // Yahoo emits null for bars it has no data for; a bar needs both a low and a high to count. It also
            // occasionally sends bars that cannot be real (zero prices, an inverted range, negative volume), which
            // would skew a day's averages and total, so those are dropped too.
            if (lows[i] is { } low && highs[i] is { } high && volumes[i] is null or >= 0 && low > 0 && low <= high)
            {
                bars.Add(new IntradayBar(
                    DateTimeOffset.FromUnixTimeSeconds(timestamps[i]),
                    low,
                    high,
                    volumes[i] ?? 0));
            }
        }

        if (bars.Count < timestamps.Count)
        {
            LogDroppedBars(logger, symbol.Value, timestamps.Count - bars.Count, timestamps.Count);
        }

        return bars;
    }

    private static TimeZoneInfo ResolveTimeZone(YahooMeta? meta, Symbol symbol, ILogger logger)
    {
        if (!string.IsNullOrWhiteSpace(meta?.ExchangeTimezoneName))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(meta.ExchangeTimezoneName);
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                // Fall through to the numeric offset Yahoo also provides.
            }
        }

        if (meta?.Gmtoffset is not { } offsetSeconds)
        {
            throw new UpstreamUnavailableException("Yahoo returned no usable exchange timezone information.");
        }

        LogTimeZoneFallback(logger, symbol.Value, meta.ExchangeTimezoneName ?? "(missing)", offsetSeconds);

        var offset = TimeSpan.FromSeconds(offsetSeconds);
        var name = $"UTC{(offset < TimeSpan.Zero ? "-" : "+")}{offset:hh\\:mm}";
        return TimeZoneInfo.CreateCustomTimeZone(name, offset, name, name);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Exchange time zone {ZoneName} is unavailable for {Symbol}; using a fixed offset of {OffsetSeconds}s that ignores daylight saving.")]
    private static partial void LogTimeZoneFallback(ILogger logger, string symbol, string zoneName, int offsetSeconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Dropped {DroppedBars} of {TotalBars} bars for {Symbol} because Yahoo reported no usable low, high or volume for them.")]
    private static partial void LogDroppedBars(ILogger logger, string symbol, int droppedBars, int totalBars);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Yahoo returned no bars at all for {Symbol}.")]
    private static partial void LogNoBars(ILogger logger, string symbol);
}
