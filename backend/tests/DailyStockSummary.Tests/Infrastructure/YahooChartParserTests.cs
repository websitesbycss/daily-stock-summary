using DailyStockSummary.Core.Exceptions;
using DailyStockSummary.Core.Models;
using DailyStockSummary.Core.Services;
using DailyStockSummary.Infrastructure.Yahoo;

namespace DailyStockSummary.Tests.Infrastructure;

public class YahooChartParserTests
{
    private static readonly Symbol Tsla = Symbol.Parse("TSLA");

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    /// <summary>Builds a Yahoo-shaped payload. Array arguments are raw JSON so tests can inject nulls.</summary>
    private static string Chart(
        string timestamps = "[1788442200,1788443100]",
        string low = "[10.5,10.25]",
        string high = "[11.5,11.25]",
        string volume = "[100,200]",
        string meta = """{"exchangeTimezoneName":"America/New_York","gmtoffset":-14400}""") =>
        $$$"""
        {"chart":{"result":[{"meta":{{{meta}}},"timestamp":{{{timestamps}}},
        "indicators":{"quote":[{"open":{{{low}}},"high":{{{high}}},"low":{{{low}}},"close":{{{low}}},"volume":{{{volume}}}}]}}],"error":null}}
        """;

    [Fact]
    public void Parse_reads_bars_and_exchange_timezone_from_a_real_yahoo_response()
    {
        var series = YahooChartParser.Parse(Tsla, Fixture("tsla-15m-two-days.json"));

        Assert.Equal(Tsla, series.Symbol);
        Assert.Equal("America/New_York", series.ExchangeTimeZone.Id);
        Assert.Equal(52, series.Bars.Count);
        Assert.Equal(
            new IntradayBar(DateTimeOffset.FromUnixTimeSeconds(1788442200), 365.9100036621094m, 375.17999267578125m, 9472614),
            series.Bars[0]);
    }

    [Fact]
    public void Real_response_summarizes_to_independently_computed_daily_values()
    {
        // Expected values were computed outside this codebase from the same fixture (Node, plain arithmetic).
        var series = YahooChartParser.Parse(Tsla, Fixture("tsla-15m-two-days.json"));

        var summaries = new DailySummaryCalculator().Calculate(series);

        Assert.Equal(
            [
                new DailySummary(new DateOnly(2026, 9, 3), 378.9251m, 381.4209m, 57_740_176),
                new DailySummary(new DateOnly(2026, 9, 4), 352.7499m, 354.7028m, 59_338_238),
            ],
            summaries);
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

    [Fact]
    public void Parse_falls_back_to_the_gmt_offset_when_the_timezone_name_is_unknown()
    {
        var json = Chart(meta: """{"exchangeTimezoneName":"Mars/Olympus_Mons","gmtoffset":-14400}""");

        var zone = YahooChartParser.Parse(Tsla, json).ExchangeTimeZone;

        Assert.Equal(TimeSpan.FromHours(-4), zone.BaseUtcOffset);
    }

    [Fact]
    public void Parse_falls_back_to_the_gmt_offset_when_the_timezone_name_is_missing()
    {
        var zone = YahooChartParser.Parse(Tsla, Chart(meta: """{"gmtoffset":32400}""")).ExchangeTimeZone;

        Assert.Equal(TimeSpan.FromHours(9), zone.BaseUtcOffset);
    }

    [Fact]
    public void Parse_rejects_a_response_with_no_usable_timezone_information()
    {
        Assert.Throws<UpstreamUnavailableException>(() => YahooChartParser.Parse(Tsla, Chart(meta: "{}")));
    }

    [Fact]
    public void Parse_returns_no_bars_when_yahoo_omits_the_timestamp_array()
    {
        const string json = """
            {"chart":{"result":[{"meta":{"exchangeTimezoneName":"America/New_York","gmtoffset":-14400},
            "indicators":{"quote":[{}]}}],"error":null}}
            """;

        Assert.Empty(YahooChartParser.Parse(Tsla, json).Bars);
    }

    [Fact]
    public void Parse_returns_no_bars_for_an_empty_timestamp_array()
    {
        const string json = """
            {"chart":{"result":[{"meta":{"exchangeTimezoneName":"America/New_York","gmtoffset":-14400},
            "timestamp":[],"indicators":{"quote":[{}]}}],"error":null}}
            """;

        Assert.Empty(YahooChartParser.Parse(Tsla, json).Bars);
    }

    [Theory]
    [InlineData("""{"gmtoffset":100000}""")] // beyond the +/-14h .NET allows
    [InlineData("""{"gmtoffset":30}""")] // not a whole number of minutes
    public void Parse_rejects_a_gmt_offset_that_cannot_be_a_timezone(string meta)
    {
        Assert.Throws<UpstreamUnavailableException>(() => YahooChartParser.Parse(Tsla, Chart(meta: meta)));
    }

    [Fact]
    public void Parse_rejects_a_timestamp_outside_the_representable_range()
    {
        var json = Chart(timestamps: "[99999999999999]", low: "[1.0]", high: "[2.0]", volume: "[1]");

        Assert.Throws<UpstreamUnavailableException>(() => YahooChartParser.Parse(Tsla, json));
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
    [InlineData("""{"chart":null}""")]
    [InlineData("""{"chart":{"result":[null],"error":null}}""")]
    [InlineData("{}")]
    public void Parse_rejects_a_response_without_a_result(string json)
    {
        Assert.Throws<UpstreamUnavailableException>(() => YahooChartParser.Parse(Tsla, json));
    }

    [Theory]
    [InlineData("[1,2]", "[1.0]", "[2.0,3.0]", "[1,2]")]
    [InlineData("[1,2]", "[1.0,1.0]", "[2.0]", "[1,2]")]
    [InlineData("[1,2]", "[1.0,1.0]", "[2.0,3.0]", "[1]")]
    [InlineData("[1,2]", "[1.0,1.0,1.0]", "[2.0,3.0]", "[1,2]")]
    [InlineData("[1,2]", "[1.0,1.0]", "[2.0,3.0,4.0]", "[1,2]")]
    [InlineData("[1,2]", "[1.0,1.0]", "[2.0,3.0]", "[1,2,3]")]
    public void Parse_rejects_quote_arrays_whose_length_differs_from_the_timestamps(
        string timestamps, string low, string high, string volume)
    {
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
    [InlineData("<html>Too Many Requests</html>")]
    [InlineData("""{"chart":{"result":[""")]
    public void Parse_wraps_malformed_json_as_upstream_unavailable(string body)
    {
        Assert.Throws<UpstreamUnavailableException>(() => YahooChartParser.Parse(Tsla, body));
    }
}
