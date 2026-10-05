using System.Net;
using DailyStockSummary.Core.Exceptions;
using DailyStockSummary.Core.Models;
using DailyStockSummary.Infrastructure.Yahoo;
using DailyStockSummary.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace DailyStockSummary.Tests.Infrastructure;

/// <summary>
/// Drives the real resilience pipeline (retry, circuit breaker, timeouts) through DI. Only the network is
/// replaced, so what is asserted is how the application behaves when Yahoo misbehaves.
/// </summary>
public class YahooResilienceTests
{
    private static readonly Symbol Tsla = Symbol.Parse("TSLA");

    private static Dictionary<string, string?> Settings(params (string Key, string Value)[] overrides)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Yahoo:Resilience:RetryBaseDelay"] = "00:00:00.001", // keep retries fast in tests
        };

        foreach (var (key, value) in overrides)
        {
            settings[$"Yahoo:Resilience:{key}"] = value;
        }

        return settings;
    }

    private static HttpResponseMessage Status(HttpStatusCode status) => new(status) { Content = new StringContent("") };

    private static HttpResponseMessage RealTeslaMonth() =>
        new(HttpStatusCode.OK) { Content = new StringContent(TestFixtures.Read(TestFixtures.TeslaOneMonth)) };

    private static YahooFinanceClient ClientFor(ServiceProvider provider) =>
        provider.GetRequiredService<YahooFinanceClient>();

    [Fact]
    public async Task Two_transient_503s_are_retried_and_the_request_then_succeeds()
    {
        var handler = StubHttpMessageHandler.Responding(call => call < 3 ? Status(HttpStatusCode.ServiceUnavailable) : RealTeslaMonth());
        using var provider = StockSummaryTestHost.Build(handler, Settings());

        var series = await ClientFor(provider).GetIntradayAsync(Tsla, CancellationToken.None);

        Assert.Equal(547, series.Bars.Count);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task A_dropped_connection_is_retried_and_the_request_then_succeeds()
    {
        var handler = StubHttpMessageHandler.Responding(call =>
        {
            if (call == 1)
            {
                throw new HttpRequestException("connection reset");
            }

            return RealTeslaMonth();
        });
        using var provider = StockSummaryTestHost.Build(handler, Settings());

        var series = await ClientFor(provider).GetIntradayAsync(Tsla, CancellationToken.None);

        Assert.Equal(547, series.Bars.Count);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task A_persistent_outage_is_retried_a_bounded_number_of_times_then_reported_as_unavailable()
    {
        var handler = StubHttpMessageHandler.Responding(_ => Status(HttpStatusCode.ServiceUnavailable));
        using var provider = StockSummaryTestHost.Build(handler, Settings());

        var ex = await Assert.ThrowsAsync<UpstreamUnavailableException>(
            () => ClientFor(provider).GetIntradayAsync(Tsla, CancellationToken.None));

        Assert.Equal(UpstreamFailureKind.Unavailable, ex.Kind);
        Assert.Equal(3, handler.Requests.Count); // the first attempt plus the default 2 retries
    }

    [Fact]
    public async Task A_rate_limit_response_is_not_retried_because_retrying_deepens_the_block()
    {
        var handler = StubHttpMessageHandler.Responding(_ => Status(HttpStatusCode.TooManyRequests));
        using var provider = StockSummaryTestHost.Build(handler, Settings());

        await Assert.ThrowsAsync<UpstreamUnavailableException>(
            () => ClientFor(provider).GetIntradayAsync(Tsla, CancellationToken.None));

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task An_unknown_symbol_is_not_retried()
    {
        const string body = """{"chart":{"result":null,"error":{"code":"Not Found","description":"No data found"}}}""";
        var handler = StubHttpMessageHandler.Responding(_ => new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent(body) });
        using var provider = StockSummaryTestHost.Build(handler, Settings());

        await Assert.ThrowsAsync<SymbolNotFoundException>(
            () => ClientFor(provider).GetIntradayAsync(Tsla, CancellationToken.None));

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task A_server_that_never_answers_times_out_every_attempt_and_is_reported_as_a_timeout()
    {
        var handler = StubHttpMessageHandler.WaitingForCancellation();
        using var provider = StockSummaryTestHost.Build(handler, Settings(("AttemptTimeout", "00:00:00.050")));

        var ex = await Assert.ThrowsAsync<UpstreamUnavailableException>(
            () => ClientFor(provider).GetIntradayAsync(Tsla, CancellationToken.None));

        Assert.Equal(UpstreamFailureKind.Timeout, ex.Kind);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task After_repeated_failures_the_circuit_opens_and_calls_fail_fast_without_reaching_yahoo()
    {
        var handler = StubHttpMessageHandler.Responding(_ => Status(HttpStatusCode.InternalServerError));
        using var provider = StockSummaryTestHost.Build(
            handler,
            Settings(("RetryCount", "0"), ("BreakerMinimumThroughput", "2")));
        var client = ClientFor(provider);

        await Assert.ThrowsAsync<UpstreamUnavailableException>(() => client.GetIntradayAsync(Tsla, CancellationToken.None));
        await Assert.ThrowsAsync<UpstreamUnavailableException>(() => client.GetIntradayAsync(Tsla, CancellationToken.None));
        var failFast = await Assert.ThrowsAsync<UpstreamUnavailableException>(
            () => client.GetIntradayAsync(Tsla, CancellationToken.None));

        Assert.Equal(UpstreamFailureKind.Unavailable, failFast.Kind);
        Assert.Equal(2, handler.Requests.Count); // the third call never reached Yahoo
    }

    [Fact]
    public async Task A_caller_cancelling_mid_request_is_not_reported_as_an_upstream_failure()
    {
        var handler = StubHttpMessageHandler.WaitingForCancellation();
        using var provider = StockSummaryTestHost.Build(handler, Settings());
        using var cts = new CancellationTokenSource();

        var pending = ClientFor(provider).GetIntradayAsync(Tsla, cts.Token);
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task A_request_timeout_408_is_retried_like_any_other_transient_failure()
    {
        var handler = StubHttpMessageHandler.Responding(call => call < 2 ? Status(HttpStatusCode.RequestTimeout) : RealTeslaMonth());
        using var provider = StockSummaryTestHost.Build(handler, Settings());

        var series = await ClientFor(provider).GetIntradayAsync(Tsla, CancellationToken.None);

        Assert.Equal(547, series.Bars.Count);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Every_retry_attempt_counts_toward_the_circuit_breaker_so_one_bad_request_can_open_it()
    {
        // Retry sits outside the breaker: one request that fails 3 times (1 + 2 retries) is 3 failures.
        var handler = StubHttpMessageHandler.Responding(_ => Status(HttpStatusCode.ServiceUnavailable));
        using var provider = StockSummaryTestHost.Build(handler, Settings(("BreakerMinimumThroughput", "3")));
        var client = ClientFor(provider);

        await Assert.ThrowsAsync<UpstreamUnavailableException>(() => client.GetIntradayAsync(Tsla, CancellationToken.None));
        await Assert.ThrowsAsync<UpstreamUnavailableException>(() => client.GetIntradayAsync(Tsla, CancellationToken.None));

        Assert.Equal(3, handler.Requests.Count); // the second request never reached Yahoo
    }

    [Fact]
    public async Task The_total_time_budget_stops_further_retries_even_when_each_attempt_is_within_its_own_limit()
    {
        var handler = StubHttpMessageHandler.WaitingForCancellation();
        using var provider = StockSummaryTestHost.Build(
            handler,
            Settings(("AttemptTimeout", "00:00:00.050"), ("TotalTimeout", "00:00:00.120"), ("RetryCount", "5")));

        var ex = await Assert.ThrowsAsync<UpstreamUnavailableException>(
            () => ClientFor(provider).GetIntradayAsync(Tsla, CancellationToken.None));

        Assert.Equal(UpstreamFailureKind.Timeout, ex.Kind);
        Assert.InRange(handler.Requests.Count, 1, 4); // without the budget all 6 attempts would run
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)] // a user typing unknown symbols is not a Yahoo outage
    [InlineData(HttpStatusCode.TooManyRequests)] // being rate limited is handled by backing off, not by tripping
    public async Task Unknown_symbols_and_rate_limit_responses_never_open_the_circuit(HttpStatusCode status)
    {
        const string notFound = """{"chart":{"result":null,"error":{"code":"Not Found","description":"No data found"}}}""";
        var handler = StubHttpMessageHandler.Responding(_ =>
            new HttpResponseMessage(status) { Content = new StringContent(status == HttpStatusCode.NotFound ? notFound : "Too Many Requests") });
        using var provider = StockSummaryTestHost.Build(handler, Settings(("RetryCount", "0"), ("BreakerMinimumThroughput", "2")));
        var client = ClientFor(provider);

        for (var i = 0; i < 4; i++)
        {
            await Assert.ThrowsAnyAsync<Exception>(() => client.GetIntradayAsync(Tsla, CancellationToken.None));
        }

        Assert.Equal(4, handler.Requests.Count); // every call still reached Yahoo: the breaker stayed closed
    }
}
