using System.Text.Json;
using DailyStockSummary.Core.Exceptions;
using DailyStockSummary.Core.Models;

namespace DailyStockSummary.Infrastructure.Yahoo;

/// <summary>Turns a Yahoo v8 chart payload into an <see cref="IntradaySeries"/> or a typed failure.</summary>
internal static class YahooChartParser
{
    private const string NotFoundCode = "Not Found";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IntradaySeries Parse(Symbol symbol, string json)
    {
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

        var zone = ResolveTimeZone(result.Meta);

        if (result.Timestamp is not { Count: > 0 } timestamps)
        {
            return new IntradaySeries(symbol, zone, []);
        }

        var quote = result.Indicators?.Quote is [var first, ..] ? first : null;

        return new IntradaySeries(symbol, zone, ReadBars(timestamps, quote));
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

    private static List<IntradayBar> ReadBars(IReadOnlyList<long> timestamps, YahooQuote? quote)
    {
        if (quote is not { Low: { } lows, High: { } highs, Volume: { } volumes }
            || lows.Count != timestamps.Count
            || highs.Count != timestamps.Count
            || volumes.Count != timestamps.Count)
        {
            throw new UpstreamUnavailableException("Yahoo returned quote data that does not line up with its timestamps.");
        }

        var bars = new List<IntradayBar>(timestamps.Count);

        try
        {
            for (var i = 0; i < timestamps.Count; i++)
            {
                // Yahoo emits null for bars it has no data for; a bar needs both a low and a high to count.
                if (lows[i] is { } low && highs[i] is { } high)
                {
                    bars.Add(new IntradayBar(
                        DateTimeOffset.FromUnixTimeSeconds(timestamps[i]),
                        low,
                        high,
                        volumes[i] ?? 0));
                }
            }
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new UpstreamUnavailableException("Yahoo returned a timestamp outside the supported range.", ex);
        }

        return bars;
    }

    private static TimeZoneInfo ResolveTimeZone(YahooMeta? meta)
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

        try
        {
            var offset = TimeSpan.FromSeconds(offsetSeconds);
            var name = $"UTC{(offset < TimeSpan.Zero ? "-" : "+")}{offset:hh\\:mm}";
            return TimeZoneInfo.CreateCustomTimeZone(name, offset, name, name);
        }
        catch (ArgumentException ex)
        {
            throw new UpstreamUnavailableException("Yahoo returned an invalid exchange timezone offset.", ex);
        }
    }
}
