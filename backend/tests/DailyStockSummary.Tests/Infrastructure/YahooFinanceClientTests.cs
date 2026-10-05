using System.Net;
using DailyStockSummary.Core.Exceptions;
using DailyStockSummary.Core.Models;
using DailyStockSummary.Infrastructure.Yahoo;
using DailyStockSummary.Tests.TestSupport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;

namespace DailyStockSummary.Tests.Infrastructure;

public class YahooFinanceClientTests
{
    private static readonly Symbol Tsla = Symbol.Parse("TSLA");

    private static readonly YahooOptions TestOptions = new()
    {
        BaseUrl = new Uri("https://yahoo.test/"),
        UserAgent = "TestAgent/1.0",
        Interval = "15m",
        Range = "1mo",
    };

    private static YahooFinanceClient ClientFor(HttpMessageHandler handler, TimeSpan? timeout = null) =>
        new(
            new HttpClient(handler) { Timeout = timeout ?? Timeout.InfiniteTimeSpan },
            Options.Create(TestOptions),
            NullLogger<YahooFinanceClient>.Instance);

    private static HttpResponseMessage Response(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body) };

    private static HttpResponseMessage RealTeslaResponse() =>
        Response(HttpStatusCode.OK, TestFixtures.Read(TestFixtures.TeslaOneMonth));

    [Fact]
    public async Task GetIntradayAsync_requests_the_chart_url_with_interval_range_and_user_agent()
    {
        var handler = StubHttpMessageHandler.Returning(RealTeslaResponse());

        await ClientFor(handler).GetIntradayAsync(Tsla, CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://yahoo.test/v8/finance/chart/TSLA?interval=15m&range=1mo", request.RequestUri!.OriginalString);
        Assert.Equal("TestAgent/1.0", request.Headers.UserAgent.ToString());
    }

    [Fact]
    public async Task GetIntradayAsync_url_encodes_symbols_with_special_characters()
    {
        var handler = StubHttpMessageHandler.Returning(RealTeslaResponse());

        await ClientFor(handler).GetIntradayAsync(Symbol.Parse("^GSPC"), CancellationToken.None);

        Assert.Equal(
            "https://yahoo.test/v8/finance/chart/%5EGSPC?interval=15m&range=1mo",
            Assert.Single(handler.Requests).RequestUri!.OriginalString);
    }

    [Fact]
    public async Task GetIntradayAsync_queries_the_last_month_of_15_minute_bars_by_default()
    {
        // The assessment asks for the last month of 15m data from Yahoo; no configuration must be needed.
        var handler = StubHttpMessageHandler.Returning(RealTeslaResponse());
        var client = new YahooFinanceClient(new HttpClient(handler), Options.Create(new YahooOptions()), NullLogger<YahooFinanceClient>.Instance);

        await client.GetIntradayAsync(Tsla, CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(
            "https://query1.finance.yahoo.com/v8/finance/chart/TSLA?interval=15m&range=1mo",
            request.RequestUri!.OriginalString);
        Assert.Contains("Mozilla/5.0", request.Headers.UserAgent.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetIntradayAsync_surfaces_data_quality_warnings_through_its_own_logger()
    {
        // Operators read the application log; a repaired response must show up there.
        var payload = YahooPayload.Chart(meta: """{"exchangeTimezoneName":"Unknown/Zone","gmtoffset":19800}""");
        var handler = StubHttpMessageHandler.Returning(Response(HttpStatusCode.OK, payload));
        var logger = new FakeLogger<YahooFinanceClient>();
        var client = new YahooFinanceClient(new HttpClient(handler), Options.Create(TestOptions), logger);

        await client.GetIntradayAsync(Tsla, CancellationToken.None);

        var record = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Contains(record.StructuredState!, pair => pair is { Key: "ZoneName", Value: "Unknown/Zone" });
    }

    [Fact]
    public async Task GetIntradayAsync_maps_a_yahoo_not_found_response_to_symbol_not_found()
    {
        const string body = """{"chart":{"result":null,"error":{"code":"Not Found","description":"No data found, symbol may be delisted"}}}""";
        var handler = StubHttpMessageHandler.Returning(Response(HttpStatusCode.NotFound, body));

        var ex = await Assert.ThrowsAsync<SymbolNotFoundException>(
            () => ClientFor(handler).GetIntradayAsync(Tsla, CancellationToken.None));

        Assert.Equal(Tsla, ex.Symbol);
    }

    [Fact]
    public async Task GetIntradayAsync_does_not_blame_the_symbol_when_a_404_is_not_from_yahoo()
    {
        // A proxy page or a wrong base URL also answers 404; that must not be reported as an unknown symbol.
        var handler = StubHttpMessageHandler.Returning(Response(HttpStatusCode.NotFound, "<html>nginx 404</html>"));

        await Assert.ThrowsAsync<UpstreamUnavailableException>(
            () => ClientFor(handler).GetIntradayAsync(Tsla, CancellationToken.None));
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, "Too Many Requests")] // Yahoo rate limiting
    [InlineData(HttpStatusCode.Forbidden, "Forbidden")] // blocked user agent
    [InlineData(HttpStatusCode.BadGateway, "<html>502 Bad Gateway</html>")]
    [InlineData(HttpStatusCode.InternalServerError, "")]
    public async Task GetIntradayAsync_maps_upstream_failure_responses_to_upstream_unavailable(HttpStatusCode status, string body)
    {
        var handler = StubHttpMessageHandler.Returning(Response(status, body));

        await Assert.ThrowsAsync<UpstreamUnavailableException>(
            () => ClientFor(handler).GetIntradayAsync(Tsla, CancellationToken.None));
    }

    [Fact]
    public async Task GetIntradayAsync_maps_a_captcha_page_served_with_http_200_to_upstream_unavailable()
    {
        var handler = StubHttpMessageHandler.Returning(Response(HttpStatusCode.OK, "<html>captcha</html>"));

        await Assert.ThrowsAsync<UpstreamUnavailableException>(
            () => ClientFor(handler).GetIntradayAsync(Tsla, CancellationToken.None));
    }

    [Fact]
    public async Task GetIntradayAsync_wraps_network_failures_as_upstream_unavailable()
    {
        var failure = new HttpRequestException("connection refused");
        var handler = StubHttpMessageHandler.Throwing(failure);

        var ex = await Assert.ThrowsAsync<UpstreamUnavailableException>(
            () => ClientFor(handler).GetIntradayAsync(Tsla, CancellationToken.None));

        Assert.Same(failure, ex.InnerException);
    }

    [Fact]
    public async Task GetIntradayAsync_reports_a_real_httpclient_timeout_as_upstream_unavailable()
    {
        // The server never answers, so HttpClient's own timeout fires while the caller's token is untouched.
        var handler = StubHttpMessageHandler.WaitingForCancellation();
        var client = ClientFor(handler, timeout: TimeSpan.FromMilliseconds(50));

        var ex = await Assert.ThrowsAsync<UpstreamUnavailableException>(
            () => client.GetIntradayAsync(Tsla, CancellationToken.None));

        Assert.Equal(UpstreamFailureKind.Timeout, ex.Kind);
    }

    [Fact]
    public async Task GetIntradayAsync_cancels_the_in_flight_request_when_the_caller_cancels()
    {
        // A client disconnecting must stop the Yahoo call. The handler only ends when the token it
        // receives fires, so this fails if the caller's token is not forwarded to the HttpClient.
        var handler = StubHttpMessageHandler.WaitingForCancellation();
        using var cts = new CancellationTokenSource();

        var pending = ClientFor(handler).GetIntradayAsync(Tsla, cts.Token);
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => pending.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "<html>nginx 404</html>")] // a proxy answering instead of Yahoo
    [InlineData(HttpStatusCode.TooManyRequests, "Too Many Requests")] // Yahoo rate limiting us
    [InlineData(HttpStatusCode.BadGateway, "<html>502 Bad Gateway</html>")]
    public async Task GetIntradayAsync_logs_which_status_yahoo_answered_with_so_operators_can_tell_failures_apart(HttpStatusCode status, string body)
    {
        var handler = StubHttpMessageHandler.Returning(Response(status, body));
        var logger = new FakeLogger<YahooFinanceClient>();
        var client = new YahooFinanceClient(new HttpClient(handler), Options.Create(TestOptions), logger);

        await Assert.ThrowsAsync<UpstreamUnavailableException>(() => client.GetIntradayAsync(Tsla, CancellationToken.None));

        var record = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Contains(record.StructuredState!, pair => pair.Key == "StatusCode" && pair.Value == ((int)status).ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.Contains(record.StructuredState!, pair => pair is { Key: "Symbol", Value: "TSLA" });
    }

    [Fact]
    public async Task GetIntradayAsync_does_not_log_a_warning_when_a_user_simply_asks_for_an_unknown_symbol()
    {
        const string body = """{"chart":{"result":null,"error":{"code":"Not Found","description":"No data found"}}}""";
        var handler = StubHttpMessageHandler.Returning(Response(HttpStatusCode.NotFound, body));
        var logger = new FakeLogger<YahooFinanceClient>();
        var client = new YahooFinanceClient(new HttpClient(handler), Options.Create(TestOptions), logger);

        await Assert.ThrowsAsync<SymbolNotFoundException>(() => client.GetIntradayAsync(Tsla, CancellationToken.None));

        Assert.DoesNotContain(logger.Collector.GetSnapshot(), record => record.Level >= LogLevel.Warning);
    }
}
