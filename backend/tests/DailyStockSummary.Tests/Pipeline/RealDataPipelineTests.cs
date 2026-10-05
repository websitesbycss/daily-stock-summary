using System.Net;
using DailyStockSummary.Core.Models;
using DailyStockSummary.Core.Services;
using DailyStockSummary.Infrastructure.Yahoo;
using DailyStockSummary.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DailyStockSummary.Tests.Pipeline;

/// <summary>
/// Runs a real month of TSLA data through the real client, parser, calculator and service. Only the
/// network is replaced. Expected values were computed outside this codebase straight from the raw JSON
/// (plain arithmetic in Node, grouped by America/New_York date), so they do not share logic with it.
/// </summary>
public class RealDataPipelineTests
{
    private static async Task<IReadOnlyList<DailySummary>> SummarizeRealMonthAsync()
    {
        var handler = StubHttpMessageHandler.Returning(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(TestFixtures.Read(TestFixtures.TeslaOneMonth)) });
        var client = new YahooFinanceClient(new HttpClient(handler), Options.Create(new YahooOptions()), NullLogger<YahooFinanceClient>.Instance);
        var service = new StockSummaryService(client, new DailySummaryCalculator());

        return await service.GetDailySummariesAsync(Symbol.Parse("tsla"), CancellationToken.None);
    }

    [Fact]
    public async Task One_summary_per_trading_day_oldest_first_with_no_weekends_or_the_labor_day_holiday()
    {
        var days = (await SummarizeRealMonthAsync()).Select(s => s.Day).ToArray();

        Assert.Equal(21, days.Length);
        Assert.Equal(days.Order(), days);
        Assert.All(days, d => Assert.NotEqual(DayOfWeek.Saturday, d.DayOfWeek));
        Assert.All(days, d => Assert.NotEqual(DayOfWeek.Sunday, d.DayOfWeek));
        Assert.DoesNotContain(new DateOnly(2026, 9, 7), days); // Labor Day: the market was closed
        Assert.Equal(new DateOnly(2026, 9, 3), days[0]);
        Assert.Equal(new DateOnly(2026, 10, 2), days[^1]);
    }

    [Fact]
    public async Task Averages_and_volume_match_values_computed_independently_from_the_raw_data()
    {
        var byDay = (await SummarizeRealMonthAsync()).ToDictionary(s => s.Day);

        Assert.Equal(new DailySummary(new DateOnly(2026, 9, 3), 378.9251m, 381.4209m, 57_740_176), byDay[new DateOnly(2026, 9, 3)]);
        Assert.Equal(new DailySummary(new DateOnly(2026, 9, 4), 352.7499m, 354.7028m, 59_338_238), byDay[new DateOnly(2026, 9, 4)]);
        // First session after the holiday weekend.
        Assert.Equal(new DailySummary(new DateOnly(2026, 9, 8), 364.7665m, 366.9835m, 46_169_150), byDay[new DateOnly(2026, 9, 8)]);
        // Yahoo appends a 27th bar (the closing print) to the most recent day; it must be counted.
        Assert.Equal(new DailySummary(new DateOnly(2026, 10, 2), 370.5468m, 372.7738m, 49_034_932), byDay[new DateOnly(2026, 10, 2)]);
    }

    [Fact]
    public async Task No_traded_volume_is_lost_or_double_counted_across_the_month()
    {
        var total = (await SummarizeRealMonthAsync()).Sum(s => s.Volume);

        Assert.Equal(735_302_439, total);
    }
}
