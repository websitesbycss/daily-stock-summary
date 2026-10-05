using System.Net;
using System.Text.Json;
using DailyStockSummary.Tests.TestSupport;
using Microsoft.Extensions.Time.Testing;

namespace DailyStockSummary.Tests.Api;

/// <summary>Cross-cutting host behavior: CORS, rate limiting, health, OpenAPI and fail-fast configuration.</summary>
public class ApiHostingTests
{
    private const string ViteOrigin = "http://localhost:5173";

    private static HttpResponseMessage RealTeslaMonth() =>
        new(HttpStatusCode.OK) { Content = new StringContent(TestFixtures.Read(TestFixtures.TeslaOneMonth)) };

    private static ApiTestApp App(IReadOnlyDictionary<string, string?>? settings = null, string environment = "Development") =>
        new(StubHttpMessageHandler.Responding(_ => RealTeslaMonth()), settings, environment);

    private static HttpRequestMessage Preflight(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/stocks/tsla/daily-summary");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        return request;
    }

    private static string? AllowedOrigin(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values) ? values.Single() : null;

    // ---- CORS

    [Fact]
    public async Task The_vite_dev_server_origin_may_call_the_api_by_default()
    {
        using var app = App();
        using var client = app.CreateClient();

        var preflight = await client.SendAsync(Preflight(ViteOrigin));
        using var get = new HttpRequestMessage(HttpMethod.Get, "/api/stocks/tsla/daily-summary");
        get.Headers.Add("Origin", ViteOrigin);
        var actual = await client.SendAsync(get);

        Assert.Equal(HttpStatusCode.NoContent, preflight.StatusCode);
        Assert.Equal(ViteOrigin, AllowedOrigin(preflight));
        Assert.Equal(ViteOrigin, AllowedOrigin(actual));
    }

    [Fact]
    public async Task An_unlisted_origin_gets_no_cors_permission()
    {
        using var app = App();
        using var client = app.CreateClient();

        var preflight = await client.SendAsync(Preflight("https://evil.example"));

        Assert.Null(AllowedOrigin(preflight));
    }

    [Fact]
    public async Task Configured_origins_replace_the_default_instead_of_adding_to_it()
    {
        using var app = App(ApiTestApp.With(("Cors:AllowedOrigins:0", "https://app.example.com")));
        using var client = app.CreateClient();

        var configured = await client.SendAsync(Preflight("https://app.example.com"));
        var formerDefault = await client.SendAsync(Preflight(ViteOrigin));

        Assert.Equal("https://app.example.com", AllowedOrigin(configured));
        Assert.Null(AllowedOrigin(formerDefault));
    }

    // ---- Rate limiting

    [Fact]
    public async Task Requests_beyond_the_limit_get_a_429_problem_with_retry_after_while_health_stays_available()
    {
        using var app = App(ApiTestApp.With(("RateLimiting:PermitLimit", "3"), ("RateLimiting:Window", "00:01:00")));
        using var client = app.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/stocks/tsla/daily-summary")).StatusCode);
        }

        var limited = await client.GetAsync("/api/stocks/tsla/daily-summary");
        var health = await client.GetAsync("/health");
        using var problem = JsonDocument.Parse(await limited.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("application/problem+json", limited.Content.Headers.ContentType?.MediaType);
        Assert.Equal("urn:daily-stock-summary:problem:rate-limited", problem.RootElement.GetProperty("type").GetString());
        Assert.InRange(int.Parse(limited.Headers.GetValues("Retry-After").Single(), System.Globalization.CultureInfo.InvariantCulture), 1, 60);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    // ---- Health and OpenAPI

    [Fact]
    public async Task Health_answers_ok_without_touching_yahoo()
    {
        var yahoo = StubHttpMessageHandler.Responding(_ => RealTeslaMonth());
        using var app = new ApiTestApp(yahoo);
        using var client = app.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(yahoo.Requests);
    }

    [Fact]
    public async Task Development_serves_an_openapi_document_whose_success_schema_matches_the_wire_contract()
    {
        using var app = App(environment: "Development");
        using var client = app.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var ok = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/stocks/{symbol}/daily-summary")
            .GetProperty("get")
            .GetProperty("responses")
            .GetProperty("200")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema");
        var itemRef = ok.GetProperty("items").GetProperty("$ref").GetString()!;
        var schemaName = itemRef[(itemRef.LastIndexOf('/') + 1)..];
        var properties = document.RootElement
            .GetProperty("components").GetProperty("schemas").GetProperty(schemaName).GetProperty("properties")
            .EnumerateObject()
            .Select(property => property.Name)
            .ToArray();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("array", ok.GetProperty("type").GetString());
        Assert.Equal(["day", "lowAverage", "highAverage", "volume"], properties);
    }

    [Fact]
    public async Task Production_does_not_expose_the_openapi_document()
    {
        using var app = App(environment: "Production");
        using var client = app.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- Fail-fast configuration

    // One row per options type is enough here: it proves each is validated at startup. The rules themselves
    // are covered in the validator tests.
    public static TheoryData<string, string, string> BadConfiguration => new()
    {
        { "Yahoo:BaseUrl", "https://proxy.example.com/yahoo", "Yahoo:BaseUrl" }, // missing trailing slash
        { "Cors:AllowedOrigins:0", "*", "Cors:AllowedOrigins" }, // a wildcard would let any site call the API
        { "RateLimiting:PermitLimit", "0", "RateLimiting:PermitLimit" },
    };

    [Theory]
    [MemberData(nameof(BadConfiguration))]
    public void Bad_configuration_stops_the_host_at_startup_and_names_the_setting(string key, string value, string expectedInMessage)
    {
        using var app = App(ApiTestApp.With((key, value)));

        var failure = Assert.ThrowsAny<Exception>(() => app.CreateClient());

        Assert.Contains(expectedInMessage, Flatten(failure), StringComparison.Ordinal);
    }

    private static string Flatten(Exception exception)
    {
        var messages = new List<string>();
        for (var current = exception; current is not null; current = current.InnerException!)
        {
            messages.Add(current.Message);
            if (current.InnerException is null)
            {
                break;
            }
        }

        return string.Join(" | ", messages);
    }

    // ---- CORS on error responses (the SPA must be able to read the problem JSON, not just see a network error)

    [Theory]
    [InlineData("invalid", HttpStatusCode.BadRequest)]
    [InlineData("unknown", HttpStatusCode.NotFound)]
    [InlineData("outage", HttpStatusCode.BadGateway)]
    [InlineData("rate-limited", HttpStatusCode.TooManyRequests)]
    public async Task Error_responses_still_carry_the_cors_header_so_the_browser_lets_the_frontend_read_them(string scenario, HttpStatusCode expected)
    {
        const string notFound = """{"chart":{"result":null,"error":{"code":"Not Found","description":"No data found"}}}""";
        var yahoo = scenario switch
        {
            "unknown" => StubHttpMessageHandler.Responding(_ => new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent(notFound) }),
            "outage" => StubHttpMessageHandler.Responding(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)),
            _ => StubHttpMessageHandler.Responding(_ => RealTeslaMonth()),
        };
        var settings = scenario == "rate-limited"
            ? ApiTestApp.With(("RateLimiting:PermitLimit", "1"), ("Yahoo:Resilience:RetryCount", "0"))
            : ApiTestApp.With(("Yahoo:Resilience:RetryCount", "0"));
        using var app = new ApiTestApp(yahoo, settings);
        using var client = app.CreateClient();
        var path = scenario == "invalid" ? "/api/stocks/%21%21%21/daily-summary" : "/api/stocks/tsla/daily-summary";

        if (scenario == "rate-limited")
        {
            await client.GetAsync(path); // use up the single permit
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Origin", ViteOrigin);
        var response = await client.SendAsync(request);

        Assert.Equal(expected, response.StatusCode);
        Assert.Equal(ViteOrigin, AllowedOrigin(response));
    }

    // ---- Rate limiting is per client address

    private static async Task<HttpStatusCode> GetAsClientAsync(HttpClient client, string address)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/stocks/tsla/daily-summary");
        request.Headers.Add(ApiTestApp.ClientAddressHeader, address);
        return (await client.SendAsync(request)).StatusCode;
    }

    [Fact]
    public async Task One_clients_burst_does_not_use_up_another_clients_allowance()
    {
        using var app = App(ApiTestApp.With(("RateLimiting:PermitLimit", "2")));
        using var client = app.CreateClient();

        Assert.Equal(HttpStatusCode.OK, await GetAsClientAsync(client, "10.0.0.1"));
        Assert.Equal(HttpStatusCode.OK, await GetAsClientAsync(client, "10.0.0.1"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await GetAsClientAsync(client, "10.0.0.1"));
        Assert.Equal(HttpStatusCode.OK, await GetAsClientAsync(client, "10.0.0.2"));
    }

    [Fact]
    public async Task A_client_seen_as_plain_ipv4_and_as_ipv4_mapped_ipv6_shares_one_allowance()
    {
        // Dual-stack servers report the same IPv4 client either way; it must not get a second allowance.
        using var app = App(ApiTestApp.With(("RateLimiting:PermitLimit", "2")));
        using var client = app.CreateClient();

        Assert.Equal(HttpStatusCode.OK, await GetAsClientAsync(client, "10.0.0.1"));
        Assert.Equal(HttpStatusCode.OK, await GetAsClientAsync(client, "::ffff:10.0.0.1"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await GetAsClientAsync(client, "10.0.0.1"));
    }

    // ---- Caching through the real container

    [Fact]
    public async Task The_configured_cache_ttl_controls_when_yahoo_is_asked_again()
    {
        var yahoo = StubHttpMessageHandler.Responding(_ => RealTeslaMonth());
        var time = new FakeTimeProvider();
        using var app = new ApiTestApp(yahoo, ApiTestApp.With(("Yahoo:CacheTtl", "00:00:30")), timeProvider: time);
        using var client = app.CreateClient();

        await client.GetAsync("/api/stocks/tsla/daily-summary");
        time.Advance(TimeSpan.FromSeconds(29));
        await client.GetAsync("/api/stocks/tsla/daily-summary");
        Assert.Single(yahoo.Requests);

        time.Advance(TimeSpan.FromSeconds(2));
        await client.GetAsync("/api/stocks/tsla/daily-summary");
        Assert.Equal(2, yahoo.Requests.Count);
    }
}
