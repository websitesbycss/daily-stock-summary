using System.Net;
using DailyStockSummary.Api.Settings;
using DailyStockSummary.Tests.TestSupport;
using Microsoft.Extensions.Options;

namespace DailyStockSummary.Tests.Api;

/// <summary>
/// Behind a reverse proxy every request arrives from the proxy's address. Rate limits must then apply per real
/// client, but only when the proxy is trusted: anyone can send an X-Forwarded-For header.
/// </summary>
public class ForwardedHeadersTests
{
    private const string Proxy = "10.0.0.9";

    private static ApiTestApp App(params (string Key, string Value)[] settings)
    {
        var yahoo = StubHttpMessageHandler.Responding(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(TestFixtures.Read(TestFixtures.TeslaOneMonth)),
        });
        return new ApiTestApp(yahoo, ApiTestApp.With([("RateLimiting:PermitLimit", "2"), .. settings]));
    }

    private static (string Key, string Value)[] TrustedProxyNetwork(string cidr = "10.0.0.0/8") =>
        [("ForwardedHeaders:Enabled", "true"), ("ForwardedHeaders:KnownNetworks:0", cidr)];

    private static async Task<HttpStatusCode> GetAsync(HttpClient client, string peer, string? forwardedFor)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/stocks/tsla/daily-summary");
        request.Headers.Add(ApiTestApp.ClientAddressHeader, peer);

        if (forwardedFor is not null)
        {
            request.Headers.Add("X-Forwarded-For", forwardedFor);
        }

        return (await client.SendAsync(request)).StatusCode;
    }

    [Theory]
    [InlineData("10.0.0.9")]
    [InlineData("::ffff:10.0.0.9")] // how a dual-stack listener (http://+:8080) reports the same IPv4 proxy
    public async Task Behind_a_trusted_proxy_each_real_client_gets_its_own_allowance(string proxy)
    {
        using var app = App(TrustedProxyNetwork());
        using var client = app.CreateClient();

        Assert.Equal(HttpStatusCode.OK, await GetAsync(client, proxy, "203.0.113.7"));
        Assert.Equal(HttpStatusCode.OK, await GetAsync(client, proxy, "203.0.113.7"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await GetAsync(client, proxy, "203.0.113.7"));
        Assert.Equal(HttpStatusCode.OK, await GetAsync(client, proxy, "203.0.113.8"));
    }

    [Fact]
    public async Task A_client_cannot_get_fresh_allowances_by_prefixing_the_forwarded_for_header()
    {
        // nginx appends the address it saw to whatever the client sent: "<spoofed>, <real client>".
        using var app = App(TrustedProxyNetwork());
        using var client = app.CreateClient();

        Assert.Equal(HttpStatusCode.OK, await GetAsync(client, Proxy, "9.9.9.1, 203.0.113.7"));
        Assert.Equal(HttpStatusCode.OK, await GetAsync(client, Proxy, "9.9.9.2, 203.0.113.7"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await GetAsync(client, Proxy, "9.9.9.3, 203.0.113.7"));
    }

    [Fact]
    public async Task A_peer_outside_the_trusted_networks_cannot_choose_its_own_address()
    {
        using var app = App(TrustedProxyNetwork());
        using var client = app.CreateClient();

        Assert.Equal(HttpStatusCode.OK, await GetAsync(client, "198.51.100.5", "1.1.1.1"));
        Assert.Equal(HttpStatusCode.OK, await GetAsync(client, "198.51.100.5", "1.1.1.2"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await GetAsync(client, "198.51.100.5", "1.1.1.3"));
    }

    [Fact]
    public async Task Forwarded_headers_are_ignored_unless_enabled()
    {
        using var app = App();
        using var client = app.CreateClient();

        Assert.Equal(HttpStatusCode.OK, await GetAsync(client, Proxy, "203.0.113.7"));
        Assert.Equal(HttpStatusCode.OK, await GetAsync(client, Proxy, "203.0.113.8"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await GetAsync(client, Proxy, "203.0.113.9"));
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    public async Task Trusting_a_network_does_not_trust_the_loopback_address_as_well(string loopback)
    {
        // Loopback is trusted by ASP.NET's defaults; only the configured networks may be trusted here.
        using var app = App(TrustedProxyNetwork());
        using var client = app.CreateClient();

        Assert.Equal(HttpStatusCode.OK, await GetAsync(client, loopback, "1.1.1.1"));
        Assert.Equal(HttpStatusCode.OK, await GetAsync(client, loopback, "1.1.1.2"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await GetAsync(client, loopback, "1.1.1.3"));
    }

    [Fact]
    public async Task By_default_only_the_nearest_proxy_hop_is_unwrapped()
    {
        // "<client>, <inner proxy>": with a limit of one, the nearest hop's entry is the client, so two different
        // clients behind the same inner proxy share one allowance.
        using var app = App(TrustedProxyNetwork());
        using var client = app.CreateClient();

        Assert.Equal(HttpStatusCode.OK, await GetAsync(client, Proxy, "203.0.113.7, 10.0.0.5"));
        Assert.Equal(HttpStatusCode.OK, await GetAsync(client, Proxy, "203.0.113.8, 10.0.0.5"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await GetAsync(client, Proxy, "203.0.113.9, 10.0.0.5"));
    }

    [Fact]
    public async Task A_larger_forward_limit_reaches_through_a_chain_of_trusted_proxies_to_the_real_client()
    {
        using var app = App([.. TrustedProxyNetwork(), ("ForwardedHeaders:ForwardLimit", "2")]);
        using var client = app.CreateClient();

        Assert.Equal(HttpStatusCode.OK, await GetAsync(client, Proxy, "203.0.113.7, 10.0.0.5"));
        Assert.Equal(HttpStatusCode.OK, await GetAsync(client, Proxy, "203.0.113.7, 10.0.0.6"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await GetAsync(client, Proxy, "203.0.113.7, 10.0.0.7"));
        Assert.Equal(HttpStatusCode.OK, await GetAsync(client, Proxy, "203.0.113.8, 10.0.0.5"));
    }

    [Theory]
    [InlineData("10.0.0.0/33")] // no such prefix length
    [InlineData("banana")]
    [InlineData("10.0.0.0")] // a bare address is not a network; /32 must be explicit
    [InlineData("")]
    public async Task A_malformed_trusted_network_stops_the_host_from_starting(string cidr)
    {
        using var app = App(TrustedProxyNetwork(cidr));

        var failure = await Assert.ThrowsAsync<OptionsValidationException>(() => Task.Run(app.CreateClient));

        Assert.Contains("ForwardedHeaders:KnownNetworks", string.Join(" ", failure.Failures), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Enabling_forwarded_headers_without_saying_which_proxies_to_trust_stops_the_host_from_starting()
    {
        using var app = App(("ForwardedHeaders:Enabled", "true"));

        var failure = await Assert.ThrowsAsync<OptionsValidationException>(() => Task.Run(app.CreateClient));

        Assert.Contains("ForwardedHeaders:KnownNetworks", string.Join(" ", failure.Failures), StringComparison.Ordinal);
    }
}
