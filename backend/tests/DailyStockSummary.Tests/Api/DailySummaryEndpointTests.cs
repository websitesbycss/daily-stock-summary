using System.Net;
using System.Text.Json;
using DailyStockSummary.Api.Errors;
using DailyStockSummary.Tests.TestSupport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace DailyStockSummary.Tests.Api;

public class DailySummaryEndpointTests
{
    private const string NotFoundBody = """{"chart":{"result":null,"error":{"code":"Not Found","description":"No data found, symbol may be delisted"}}}""";

    private static HttpResponseMessage RealTeslaMonth() =>
        new(HttpStatusCode.OK) { Content = new StringContent(TestFixtures.Read(TestFixtures.TeslaOneMonth)) };

    private static async Task<JsonElement> ReadProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    [Fact]
    public async Task Returns_the_exact_json_shape_the_assessment_asks_for_from_real_yahoo_data()
    {
        // Expected values were computed independently from the raw Yahoo JSON (see RealDataPipelineTests).
        using var app = new ApiTestApp(StubHttpMessageHandler.Responding(_ => RealTeslaMonth()));
        using var client = app.CreateClient();

        var response = await client.GetAsync("/api/stocks/tsla/daily-summary");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith(
            """[{"day":"2026-09-03","lowAverage":378.9251,"highAverage":381.4209,"volume":57740176},"""
            + """{"day":"2026-09-04","lowAverage":352.7499,"highAverage":354.7028,"volume":59338238},""",
            body,
            StringComparison.Ordinal);
        Assert.EndsWith(
            """{"day":"2026-10-02","lowAverage":370.5468,"highAverage":372.7738,"volume":49034932}]""",
            body,
            StringComparison.Ordinal);
        using var document = JsonDocument.Parse(body);
        Assert.Equal(21, document.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task Normalizes_the_symbol_before_asking_yahoo()
    {
        var yahoo = StubHttpMessageHandler.Responding(_ => RealTeslaMonth());
        using var app = new ApiTestApp(yahoo);
        using var client = app.CreateClient();

        await client.GetAsync("/api/stocks/%20tsla/daily-summary");

        Assert.Contains("/v8/finance/chart/TSLA?", Assert.Single(yahoo.Requests).RequestUri!.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_same_symbol_in_different_cases_is_one_cache_entry_and_one_yahoo_call()
    {
        var yahoo = StubHttpMessageHandler.Responding(_ => RealTeslaMonth());
        using var app = new ApiTestApp(yahoo);
        using var client = app.CreateClient();

        var lower = await client.GetStringAsync("/api/stocks/tsla/daily-summary");
        var upper = await client.GetStringAsync("/api/stocks/TSLA/daily-summary");

        Assert.Equal(lower, upper);
        Assert.Single(yahoo.Requests);
    }

    [Theory]
    [InlineData("%21%21%21")] // "!!!"
    [InlineData("ABCDEFGHIJKLMNOP")] // 16 characters
    [InlineData("%C5%BFpy")] // non-ASCII lookalike
    public async Task Rejects_an_invalid_symbol_with_a_400_problem_and_never_calls_yahoo(string symbol)
    {
        var yahoo = StubHttpMessageHandler.Responding(_ => RealTeslaMonth());
        using var app = new ApiTestApp(yahoo);
        using var client = app.CreateClient();

        var response = await client.GetAsync($"/api/stocks/{symbol}/daily-summary");
        var problem = await ReadProblemAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(400, problem.GetProperty("status").GetInt32());
        Assert.Equal("urn:daily-stock-summary:problem:invalid-symbol", problem.GetProperty("type").GetString());
        Assert.Empty(yahoo.Requests);
    }

    [Fact]
    public async Task Reports_an_unknown_symbol_as_a_404_problem_naming_the_symbol()
    {
        var yahoo = StubHttpMessageHandler.Responding(_ => new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent(NotFoundBody) });
        using var app = new ApiTestApp(yahoo);
        using var client = app.CreateClient();

        var response = await client.GetAsync("/api/stocks/zzzzzzzz/daily-summary");
        var problem = await ReadProblemAsync(response);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("urn:daily-stock-summary:problem:symbol-not-found", problem.GetProperty("type").GetString());
        Assert.Contains("ZZZZZZZZ", problem.GetProperty("detail").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reports_a_yahoo_outage_as_a_502_problem_without_leaking_what_yahoo_said()
    {
        var yahoo = StubHttpMessageHandler.Responding(_ =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("internal-host-db7.corp upstream trace 0xDEADBEEF") });
        using var app = new ApiTestApp(yahoo);
        using var client = app.CreateClient();

        var response = await client.GetAsync("/api/stocks/tsla/daily-summary");
        var raw = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Contains("urn:daily-stock-summary:problem:upstream-unavailable", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("0xDEADBEEF", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("internal-host-db7", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("DailyStockSummary.Infrastructure", raw, StringComparison.Ordinal); // no stack trace
    }

    [Fact]
    public async Task Reports_a_yahoo_that_never_answers_as_a_504_problem()
    {
        using var app = new ApiTestApp(
            StubHttpMessageHandler.WaitingForCancellation(),
            ApiTestApp.With(("Yahoo:Resilience:AttemptTimeout", "00:00:00.050"), ("Yahoo:Resilience:RetryCount", "0")));
        using var client = app.CreateClient();

        var response = await client.GetAsync("/api/stocks/tsla/daily-summary");
        var problem = await ReadProblemAsync(response);

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        Assert.Equal("urn:daily-stock-summary:problem:upstream-timeout", problem.GetProperty("type").GetString());
    }

    [Fact]
    public async Task An_unexpected_bug_becomes_a_generic_500_problem_that_leaks_nothing()
    {
        var yahoo = StubHttpMessageHandler.Throwing(new InvalidOperationException("password=hunter2 in connection string"));
        using var app = new ApiTestApp(yahoo, ApiTestApp.With(("Yahoo:Resilience:RetryCount", "0")));
        using var client = app.CreateClient();

        var response = await client.GetAsync("/api/stocks/tsla/daily-summary");
        var raw = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("urn:daily-stock-summary:problem:unexpected-error", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", raw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unknown_routes_and_wrong_methods_also_answer_with_problem_json()
    {
        using var app = new ApiTestApp(StubHttpMessageHandler.Responding(_ => RealTeslaMonth()));
        using var client = app.CreateClient();

        var missing = await client.GetAsync("/api/nope");
        var wrongMethod = await client.PostAsync("/api/stocks/tsla/daily-summary", content: null);

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("application/problem+json", missing.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, wrongMethod.StatusCode);
        Assert.Equal("application/problem+json", wrongMethod.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("%5EGSPC", "%5EGSPC")] // ^GSPC (S&P 500 index), as a client that escapes everything sends it
    [InlineData("^GSPC", "%5EGSPC")] // the same symbol typed as is
    [InlineData("EURUSD%3DX", "EURUSD%3DX")] // EURUSD=X (currency pair)
    [InlineData("EURUSD=X", "EURUSD%3DX")] // browsers send = unescaped
    [InlineData("BRK-B", "BRK-B")]
    [InlineData("VOD.L", "VOD.L")] // London listing
    public async Task Symbols_with_special_characters_reach_yahoo_correctly_escaped(string routeSymbol, string expectedInYahooUrl)
    {
        var yahoo = StubHttpMessageHandler.Responding(_ => RealTeslaMonth());
        using var app = new ApiTestApp(yahoo);
        using var client = app.CreateClient();

        var response = await client.GetAsync($"/api/stocks/{routeSymbol}/daily-summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            $"/v8/finance/chart/{expectedInYahooUrl}?interval=15m&range=1mo",
            Assert.Single(yahoo.Requests).RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task A_200_response_that_says_the_symbol_was_not_found_becomes_a_404_problem()
    {
        // Yahoo reports some unknown symbols in the body with a 200 status instead of a 404 status.
        const string body = """{"chart":{"result":null,"error":{"code":"Not Found","description":"No data found, symbol may be delisted"}}}""";
        using var app = new ApiTestApp(StubHttpMessageHandler.Responding(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) }));
        using var client = app.CreateClient();

        var response = await client.GetAsync("/api/stocks/zzzzzzzz/daily-summary");
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("urn:daily-stock-summary:problem:symbol-not-found", problem.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public async Task A_200_response_with_an_empty_result_list_becomes_a_502_problem()
    {
        using var app = new ApiTestApp(StubHttpMessageHandler.Responding(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"chart":{"result":[],"error":null}}""") }));
        using var client = app.CreateClient();

        var response = await client.GetAsync("/api/stocks/tsla/daily-summary");
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("urn:daily-stock-summary:problem:upstream-unavailable", problem.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public async Task A_24_hour_market_is_bucketed_by_calendar_day_in_its_own_timezone_including_partial_edge_days()
    {
        // Real BTC-USD month (UTC calendar). Expected values were computed independently from the raw JSON.
        // The first day has a single bar and no volume (the window starts mid-day); the latest day has 97 bars.
        using var app = new ApiTestApp(StubHttpMessageHandler.Responding(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(TestFixtures.Read(TestFixtures.BitcoinOneMonth)) }));
        using var client = app.CreateClient();

        using var document = JsonDocument.Parse(await client.GetStringAsync("/api/stocks/btc-usd/daily-summary"));
        var days = document.RootElement.EnumerateArray().ToDictionary(
            row => row.GetProperty("day").GetString()!,
            row => (Low: row.GetProperty("lowAverage").GetDecimal(), High: row.GetProperty("highAverage").GetDecimal(), Volume: row.GetProperty("volume").GetInt64()));

        Assert.Equal(31, days.Count);
        Assert.Equal((79639.7969m, 79679.9922m, 0L), days["2026-09-04"]);
        Assert.Equal((79687.4732m, 79773.3461m, 4_041_500_672L), days["2026-09-05"]);
        Assert.Equal((79822.6481m, 79932.4923m, 8_393_613_312L), days["2026-09-06"]);
        Assert.Equal((84677.8942m, 84744.0311m, 3_356_882_944L), days["2026-10-03"]);
        Assert.Equal((85222.7363m, 85328.7582m, 9_009_204_224L), days["2026-10-04"]);
        Assert.Equal(543_077_933_056L, days.Values.Sum(day => day.Volume));
    }

    [Fact]
    public async Task A_client_that_only_accepts_html_still_gets_the_right_status_instead_of_a_500()
    {
        using var app = new ApiTestApp(StubHttpMessageHandler.Responding(_ => RealTeslaMonth()));
        using var client = app.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/stocks/%21%21%21/daily-summary");
        request.Headers.Add("Accept", "text/html");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static List<FakeLogRecord> HandlerLogs(ApiTestApp app) =>
        app.LogProvider.Collector.GetSnapshot()
            .Where(record => record.Category == typeof(GlobalExceptionHandler).FullName)
            .ToList();

    [Fact]
    public async Task An_unexpected_bug_is_logged_at_error_with_its_exception_even_though_the_response_hides_it()
    {
        var yahoo = StubHttpMessageHandler.Throwing(new InvalidOperationException("password=hunter2 in connection string"));
        using var app = new ApiTestApp(yahoo, ApiTestApp.With(("Yahoo:Resilience:RetryCount", "0")));
        using var client = app.CreateClient();

        await client.GetAsync("/api/stocks/tsla/daily-summary");

        var record = Assert.Single(HandlerLogs(app), r => r.Level == LogLevel.Error);
        Assert.IsType<InvalidOperationException>(record.Exception);
    }

    [Fact]
    public async Task A_yahoo_outage_is_logged_as_a_warning_with_its_failure_kind()
    {
        var yahoo = StubHttpMessageHandler.Responding(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        using var app = new ApiTestApp(yahoo, ApiTestApp.With(("Yahoo:Resilience:RetryCount", "0")));
        using var client = app.CreateClient();

        await client.GetAsync("/api/stocks/tsla/daily-summary");

        var record = Assert.Single(HandlerLogs(app), r => r.Level == LogLevel.Warning);
        Assert.Contains(record.StructuredState!, pair => pair is { Key: "Kind", Value: "Unavailable" });
        Assert.NotNull(record.Exception);
    }

    [Theory]
    [InlineData("%21%21%21", HttpStatusCode.BadRequest)]
    [InlineData("zzzzzzzz", HttpStatusCode.NotFound)]
    public async Task User_mistakes_are_not_logged_as_warnings_or_errors(string symbol, HttpStatusCode expected)
    {
        var yahoo = StubHttpMessageHandler.Responding(_ => new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent(NotFoundBody) });
        using var app = new ApiTestApp(yahoo);
        using var client = app.CreateClient();

        var response = await client.GetAsync($"/api/stocks/{symbol}/daily-summary");

        Assert.Equal(expected, response.StatusCode);
        Assert.DoesNotContain(HandlerLogs(app), record => record.Level >= LogLevel.Warning);
    }
}
