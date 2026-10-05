using DailyStockSummary.Core.Models;
using DailyStockSummary.Infrastructure.Yahoo;
using DailyStockSummary.Tests.TestSupport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using static DailyStockSummary.Tests.TestSupport.YahooPayload;

namespace DailyStockSummary.Tests.Infrastructure;

/// <summary>
/// The parser quietly repairs two kinds of imperfect Yahoo data. Operators must be able to see when that
/// happens (for example to alert on it), so each repair has to leave a structured warning behind.
/// </summary>
public class YahooChartParserLoggingTests
{
    private static readonly Symbol Tsla = Symbol.Parse("TSLA");

    private static bool Has(FakeLogRecord record, string key, string value) =>
        record.StructuredState!.Any(pair => pair.Key == key && pair.Value == value);

    [Fact]
    public void Using_the_gmt_offset_instead_of_the_named_timezone_is_warned_about_with_zone_and_symbol()
    {
        // This is what happens on a host without tzdata/ICU: every exchange zone becomes a fixed offset,
        // which mis-buckets days around daylight saving changes and for near-24h markets.
        var logger = new FakeLogger();
        var json = Chart(meta: """{"exchangeTimezoneName":"Unknown/Zone","gmtoffset":19800}""");

        YahooChartParser.Parse(Tsla, json, logger);

        var record = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.True(Has(record, "ZoneName", "Unknown/Zone"));
        Assert.True(Has(record, "Symbol", "TSLA"));
    }

    [Fact]
    public void Dropping_incomplete_bars_is_warned_about_with_how_many_of_how_many()
    {
        var logger = new FakeLogger();
        var json = Chart(
            timestamps: "[1,2,3,4]",
            low: "[1.0,null,3.0,4.0]",
            high: "[2.0,2.5,null,5.0]",
            volume: "[10,20,30,40]");

        YahooChartParser.Parse(Tsla, json, logger);

        var record = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.True(Has(record, "DroppedBars", "2"));
        Assert.True(Has(record, "TotalBars", "4"));
        Assert.True(Has(record, "Symbol", "TSLA"));
    }

    [Fact]
    public void Dropping_bars_that_cannot_be_real_is_counted_in_the_same_warning()
    {
        var logger = new FakeLogger();
        var json = Chart(
            timestamps: "[1,2,3,4]",
            low: "[1.0,null,0,4.0]", // one missing, one zero price
            high: "[2.0,2.5,3.0,5.0]",
            volume: "[10,20,30,-5]"); // and one negative volume

        YahooChartParser.Parse(Tsla, json, logger);

        var record = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.True(Has(record, "DroppedBars", "3"));
        Assert.True(Has(record, "TotalBars", "4"));
    }

    [Fact]
    public void A_clean_real_response_produces_no_warnings()
    {
        var logger = new FakeLogger();

        YahooChartParser.Parse(Tsla, TestFixtures.Read(TestFixtures.TeslaOneMonth), logger);

        Assert.DoesNotContain(logger.Collector.GetSnapshot(), record => record.Level >= LogLevel.Warning);
    }

    [Theory]
    [InlineData("""{"chart":{"result":[{"meta":{"exchangeTimezoneName":"America/New_York","gmtoffset":-14400},"indicators":{"quote":[{}]}}],"error":null}}""")]
    [InlineData("""{"chart":{"result":[{"meta":{"exchangeTimezoneName":"America/New_York","gmtoffset":-14400},"timestamp":[],"indicators":{"quote":[{}]}}],"error":null}}""")]
    public void A_response_with_no_bars_at_all_is_warned_about_because_it_may_be_a_yahoo_glitch(string json)
    {
        var logger = new FakeLogger();

        var series = YahooChartParser.Parse(Tsla, json, logger);

        Assert.Empty(series.Bars);
        var record = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.True(Has(record, "Symbol", "TSLA"));
    }
}
