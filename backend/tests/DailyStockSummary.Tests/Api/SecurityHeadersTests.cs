using System.Net;
using DailyStockSummary.Tests.TestSupport;

namespace DailyStockSummary.Tests.Api;

/// <summary>The API returns JSON only, so browsers are told not to sniff it, frame it, or leak the page URL through it.</summary>
public class SecurityHeadersTests
{
    [Theory]
    [InlineData("success", HttpStatusCode.OK)]
    [InlineData("invalid", HttpStatusCode.BadRequest)]
    [InlineData("unknown", HttpStatusCode.NotFound)]
    [InlineData("outage", HttpStatusCode.BadGateway)]
    [InlineData("rate-limited", HttpStatusCode.TooManyRequests)]
    [InlineData("health", HttpStatusCode.OK)]
    public async Task Every_response_carries_the_hardening_headers_even_when_it_is_an_error(string scenario, HttpStatusCode expected)
    {
        const string notFound = """{"chart":{"result":null,"error":{"code":"Not Found","description":"No data found"}}}""";
        var yahoo = scenario switch
        {
            "unknown" => StubHttpMessageHandler.Responding(_ => new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent(notFound) }),
            "outage" => StubHttpMessageHandler.Responding(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)),
            _ => StubHttpMessageHandler.Responding(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(TestFixtures.Read(TestFixtures.TeslaOneMonth)) }),
        };
        var settings = ApiTestApp.With(("Yahoo:Resilience:RetryCount", "0"), ("RateLimiting:PermitLimit", scenario == "rate-limited" ? "1" : "100"));
        using var app = new ApiTestApp(yahoo, settings);
        using var client = app.CreateClient();
        var path = scenario switch
        {
            "invalid" => "/api/stocks/%21%21%21/daily-summary",
            "health" => "/health",
            _ => "/api/stocks/tsla/daily-summary",
        };

        if (scenario == "rate-limited")
        {
            await client.GetAsync(path); // use up the single permit
        }

        var response = await client.GetAsync(path);

        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("nosniff", Single(response, "X-Content-Type-Options"));
        Assert.Equal("no-referrer", Single(response, "Referrer-Policy"));
        Assert.Equal("default-src 'none'; frame-ancestors 'none'", Single(response, "Content-Security-Policy"));
    }

    private static string Single(HttpResponseMessage response, string header) =>
        response.Headers.TryGetValues(header, out var values)
            ? values.Single()
            : throw new Xunit.Sdk.XunitException($"Missing response header {header}.");
}
