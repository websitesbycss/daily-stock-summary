using DailyStockSummary.Core.Exceptions;
using DailyStockSummary.Core.Models;
using DailyStockSummary.Infrastructure.Yahoo;
using DailyStockSummary.Tests.TestSupport;
using static DailyStockSummary.Tests.TestSupport.YahooPayload;

namespace DailyStockSummary.Tests.Infrastructure;

public class YahooChartParserTests
{
    private static readonly Symbol Tsla = Symbol.Parse("TSLA");

    [Fact]
    public void Parse_reads_bars_and_exchange_timezone_from_a_real_yahoo_response()
    {
        var series = YahooChartParser.Parse(Tsla, TestFixtures.Read(TestFixtures.TeslaOneMonth));

        Assert.Equal(Tsla, series.Symbol);
        Assert.Equal("America/New_York", series.ExchangeTimeZone.Id);
        Assert.Equal(547, series.Bars.Count);
        Assert.Equal(
            new IntradayBar(DateTimeOffset.FromUnixTimeSeconds(1788442200), 365.9100036621094m, 375.17999267578125m, 9472614),
            series.Bars[0]);
    }

    [Fact]
    public void Parse_drops_bars_with_a_null_low_or_null_high()
    {
        var json = Chart(
            timestamps: "[1,2,3,4]",
            low: "[1.0,null,3.0,4.0]",
            high: "[2.0,2.5,null,5.0]",
            volume: "[10,20,30,40]");

        var bars = YahooChartParser.Parse(Tsla, json).Bars;

        Assert.Equal(
            [
                new IntradayBar(DateTimeOffset.FromUnixTimeSeconds(1), 1.0m, 2.0m, 10),
                new IntradayBar(DateTimeOffset.FromUnixTimeSeconds(4), 4.0m, 5.0m, 40),
            ],
            bars);
    }

    [Fact]
    public void Parse_counts_a_null_volume_as_zero()
    {
        var bars = YahooChartParser.Parse(Tsla, Chart(volume: "[null,200]")).Bars;

        Assert.Equal([0L, 200L], bars.Select(b => b.Volume));
    }

    [Theory]
    [InlineData("""{"exchangeTimezoneName":"Unknown/Zone","gmtoffset":19800}""", 5.5)] // India: a half-hour offset
    [InlineData("""{"gmtoffset":32400}""", 9.0)] // Japan, name omitted
    public void Parse_falls_back_to_the_gmt_offset_when_the_timezone_name_is_unusable(string meta, double expectedHours)
    {
        var zone = YahooChartParser.Parse(Tsla, Chart(meta: meta)).ExchangeTimeZone;

        Assert.Equal(TimeSpan.FromHours(expectedHours), zone.BaseUtcOffset);
    }

    [Fact]
    public void Parse_rejects_a_response_with_no_usable_timezone_information()
    {
        Assert.Throws<UpstreamUnavailableException>(() => YahooChartParser.Parse(Tsla, Chart(meta: "{}")));
    }

    [Theory]
    [InlineData("""{"chart":{"result":[{"meta":{"exchangeTimezoneName":"America/New_York","gmtoffset":-14400},"indicators":{"quote":[{}]}}],"error":null}}""")]
    [InlineData("""{"chart":{"result":[{"meta":{"exchangeTimezoneName":"America/New_York","gmtoffset":-14400},"timestamp":[],"indicators":{"quote":[{}]}}],"error":null}}""")]
    public void Parse_returns_no_bars_when_yahoo_reports_no_timestamps(string json)
    {
        Assert.Empty(YahooChartParser.Parse(Tsla, json).Bars);
    }

    [Fact]
    public void Parse_maps_a_not_found_error_to_symbol_not_found()
    {
        const string json = """
            {"chart":{"result":null,"error":{"code":"Not Found","description":"No data found, symbol may be delisted"}}}
            """;

        var ex = Assert.Throws<SymbolNotFoundException>(() => YahooChartParser.Parse(Tsla, json));

        Assert.Equal(Tsla, ex.Symbol);
    }

    [Fact]
    public void Parse_maps_any_other_error_to_upstream_unavailable()
    {
        const string json = """{"chart":{"result":null,"error":{"code":"Internal Server Error","description":"boom"}}}""";

        Assert.Throws<UpstreamUnavailableException>(() => YahooChartParser.Parse(Tsla, json));
    }

    [Theory]
    [InlineData("""{"chart":{"result":[],"error":null}}""")]
    [InlineData("""{"chart":{"result":null,"error":null}}""")]
    [InlineData("{}")]
    public void Parse_rejects_a_response_without_a_result(string json)
    {
        Assert.Throws<UpstreamUnavailableException>(() => YahooChartParser.Parse(Tsla, json));
    }

    [Theory]
    [InlineData("[1,2]", "[1.0]", "[2.0,3.0]", "[1,2]")] // lows truncated
    [InlineData("[1,2]", "[1.0,1.0]", "[2.0]", "[1,2]")] // highs truncated
    [InlineData("[1,2]", "[1.0,1.0]", "[2.0,3.0]", "[1]")] // volumes truncated
    public void Parse_rejects_quote_arrays_that_no_longer_line_up_with_the_timestamps(
        string timestamps, string low, string high, string volume)
    {
        // Pairing a bar's low with another bar's timestamp would silently produce wrong averages.
        var json = Chart(timestamps: timestamps, low: low, high: high, volume: volume);

        Assert.Throws<UpstreamUnavailableException>(() => YahooChartParser.Parse(Tsla, json));
    }

    [Fact]
    public void Parse_rejects_a_response_with_timestamps_but_no_quote_data()
    {
        const string json = """
            {"chart":{"result":[{"meta":{"exchangeTimezoneName":"America/New_York","gmtoffset":-14400},
            "timestamp":[1,2],"indicators":{}}],"error":null}}
            """;

        Assert.Throws<UpstreamUnavailableException>(() => YahooChartParser.Parse(Tsla, json));
    }

    [Theory]
    [InlineData("")]
    [InlineData("<html>Too Many Requests</html>")] // rate-limit/captcha page served with HTTP 200
    [InlineData("""{"chart":{"result":[""")] // connection cut mid-body
    public void Parse_wraps_malformed_json_as_upstream_unavailable(string body)
    {
        Assert.Throws<UpstreamUnavailableException>(() => YahooChartParser.Parse(Tsla, body));
    }
}
