using System.Net;
using DailyStockSummary.Core.Exceptions;
using DailyStockSummary.Core.Models;
using DailyStockSummary.Infrastructure.Yahoo;
using Microsoft.Extensions.Options;

namespace DailyStockSummary.Tests.Infrastructure;

public class YahooFinanceClientTests
{
    private static readonly Symbol Tsla = Symbol.Parse("TSLA");

    private static readonly YahooOptions Options = new()
    {
        BaseUrl = new Uri("https://yahoo.test/"),
        UserAgent = "TestAgent/1.0",
        Interval = "15m",
        Range = "1mo",
    };

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static YahooFinanceClient ClientFor(StubHandler handler) =>
        new(new HttpClient(handler), Microsoft.Extensions.Options.Options.Create(Options));

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body) };

    [Fact]
    public async Task GetIntradayAsync_requests_the_chart_url_with_interval_range_and_user_agent()
    {
        var handler = StubHandler.Returning(Json(HttpStatusCode.OK, Fixture("tsla-15m-two-days.json")));

        await ClientFor(handler).GetIntradayAsync(Tsla, CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://yahoo.test/v8/finance/chart/TSLA?interval=15m&range=1mo", request.RequestUri!.OriginalString);
        Assert.Equal("TestAgent/1.0", request.Headers.UserAgent.ToString());
    }

    [Fact]
    public async Task GetIntradayAsync_url_encodes_symbols_with_special_characters()
    {
        var handler = StubHandler.Returning(Json(HttpStatusCode.OK, Fixture("tsla-15m-two-days.json")));

        await ClientFor(handler).GetIntradayAsync(Symbol.Parse("^GSPC"), CancellationToken.None);

        Assert.Equal(
            "https://yahoo.test/v8/finance/chart/%5EGSPC?interval=15m&range=1mo",
            Assert.Single(handler.Requests).RequestUri!.OriginalString);
    }

    [Fact]
    public async Task GetIntradayAsync_returns_the_parsed_series_on_success()
    {
        var handler = StubHandler.Returning(Json(HttpStatusCode.OK, Fixture("tsla-15m-two-days.json")));

        var series = await ClientFor(handler).GetIntradayAsync(Tsla, CancellationToken.None);

        Assert.Equal(Tsla, series.Symbol);
        Assert.Equal(52, series.Bars.Count);
    }

    [Fact]
    public async Task GetIntradayAsync_maps_http_404_to_symbol_not_found()
    {
        const string body = """{"chart":{"result":null,"error":{"code":"Not Found","description":"No data found"}}}""";
        var handler = StubHandler.Returning(Json(HttpStatusCode.NotFound, body));

        var ex = await Assert.ThrowsAsync<SymbolNotFoundException>(
            () => ClientFor(handler).GetIntradayAsync(Tsla, CancellationToken.None));

        Assert.Equal(Tsla, ex.Symbol);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task GetIntradayAsync_maps_other_failure_statuses_to_upstream_unavailable(HttpStatusCode status)
    {
        var handler = StubHandler.Returning(Json(status, "upstream says no"));

        await Assert.ThrowsAsync<UpstreamUnavailableException>(
            () => ClientFor(handler).GetIntradayAsync(Tsla, CancellationToken.None));
    }

    [Fact]
    public async Task GetIntradayAsync_maps_http_404_without_a_not_found_chart_error_to_upstream_unavailable()
    {
        // A proxy page or wrong base URL also answers 404; that must not be blamed on the user's symbol.
        var handler = StubHandler.Returning(Json(HttpStatusCode.NotFound, "<html>nginx 404</html>"));

        await Assert.ThrowsAsync<UpstreamUnavailableException>(
            () => ClientFor(handler).GetIntradayAsync(Tsla, CancellationToken.None));
    }

    [Fact]
    public async Task GetIntradayAsync_does_not_treat_a_server_error_with_a_valid_chart_body_as_data()
    {
        var handler = StubHandler.Returning(Json(HttpStatusCode.InternalServerError, Fixture("tsla-15m-two-days.json")));

        await Assert.ThrowsAsync<UpstreamUnavailableException>(
            () => ClientFor(handler).GetIntradayAsync(Tsla, CancellationToken.None));
    }

    [Fact]
    public async Task GetIntradayAsync_does_not_treat_a_server_error_with_a_not_found_body_as_symbol_not_found()
    {
        const string body = """{"chart":{"result":null,"error":{"code":"Not Found","description":"No data found"}}}""";
        var handler = StubHandler.Returning(Json(HttpStatusCode.InternalServerError, body));

        await Assert.ThrowsAsync<UpstreamUnavailableException>(
            () => ClientFor(handler).GetIntradayAsync(Tsla, CancellationToken.None));
    }

    [Fact]
    public async Task GetIntradayAsync_uses_the_documented_yahoo_defaults_when_not_configured()
    {
        var handler = StubHandler.Returning(Json(HttpStatusCode.OK, Fixture("tsla-15m-two-days.json")));
        var client = new YahooFinanceClient(
            new HttpClient(handler),
            Microsoft.Extensions.Options.Options.Create(new YahooOptions()));

        await client.GetIntradayAsync(Tsla, CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(
            "https://query1.finance.yahoo.com/v8/finance/chart/TSLA?interval=15m&range=1mo",
            request.RequestUri!.OriginalString);
        Assert.Contains("Mozilla/5.0", request.Headers.UserAgent.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetIntradayAsync_wraps_network_failures_as_upstream_unavailable()
    {
        var failure = new HttpRequestException("connection refused");
        var handler = StubHandler.Throwing(failure);

        var ex = await Assert.ThrowsAsync<UpstreamUnavailableException>(
            () => ClientFor(handler).GetIntradayAsync(Tsla, CancellationToken.None));

        Assert.Same(failure, ex.InnerException);
    }

    [Fact]
    public async Task GetIntradayAsync_treats_a_request_timeout_as_upstream_unavailable()
    {
        // HttpClient reports its own timeout as TaskCanceledException while the caller's token is untouched.
        var handler = StubHandler.Throwing(new TaskCanceledException("The request timed out."));

        await Assert.ThrowsAsync<UpstreamUnavailableException>(
            () => ClientFor(handler).GetIntradayAsync(Tsla, CancellationToken.None));
    }

    [Fact]
    public async Task GetIntradayAsync_propagates_cancellation_requested_by_the_caller()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var handler = StubHandler.Throwing(new TaskCanceledException("canceled"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => ClientFor(handler).GetIntradayAsync(Tsla, cts.Token));
    }

    [Fact]
    public async Task GetIntradayAsync_cancels_the_in_flight_request_when_the_caller_cancels()
    {
        // The handler only stops when the token it is handed fires, so this fails if the caller's token
        // is not forwarded to the HttpClient.
        var handler = StubHandler.WaitingForCancellation();
        using var cts = new CancellationTokenSource();

        var pending = ClientFor(handler).GetIntradayAsync(Tsla, cts.Token);
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => pending.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task GetIntradayAsync_maps_a_malformed_success_body_to_upstream_unavailable()
    {
        var handler = StubHandler.Returning(Json(HttpStatusCode.OK, "<html>captcha</html>"));

        await Assert.ThrowsAsync<UpstreamUnavailableException>(
            () => ClientFor(handler).GetIntradayAsync(Tsla, CancellationToken.None));
    }

    /// <summary>A real <see cref="HttpMessageHandler"/> that records requests and replies from a script.</summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<CancellationToken, Task<HttpResponseMessage>> _respond;

        private StubHandler(Func<CancellationToken, Task<HttpResponseMessage>> respond) => _respond = respond;

        public List<HttpRequestMessage> Requests { get; } = [];

        public static StubHandler Returning(HttpResponseMessage response) =>
            new(_ => Task.FromResult(response));

        public static StubHandler Throwing(Exception exception) =>
            new(_ => throw exception);

        public static StubHandler WaitingForCancellation() =>
            new(async token =>
            {
                await Task.Delay(Timeout.Infinite, token);
                throw new InvalidOperationException("unreachable");
            });

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return _respond(cancellationToken);
        }
    }
}
