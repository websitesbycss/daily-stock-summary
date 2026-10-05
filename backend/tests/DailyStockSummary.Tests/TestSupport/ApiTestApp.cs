using System.Net;
using DailyStockSummary.Infrastructure.Yahoo;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace DailyStockSummary.Tests.TestSupport;

/// <summary>
/// The real API host with only Yahoo's network replaced, so requests pass through routing, validation, rate
/// limiting, CORS, caching, resilience, the client, the parser and the calculator exactly as in production.
/// </summary>
public sealed class ApiTestApp : IDisposable
{
    /// <summary>TestServer has no network peer; send this header to simulate the client's address.</summary>
    public const string ClientAddressHeader = "X-Test-Client-IP";

    private readonly WebApplicationFactory<Program> _factory;

    public ApiTestApp(
        StubHttpMessageHandler yahoo,
        IReadOnlyDictionary<string, string?>? settings = null,
        string environment = "Development",
        TimeProvider? timeProvider = null,
        Action<IServiceCollection>? configureServices = null)
    {
        var merged = new Dictionary<string, string?>
        {
            // Keep retry back-off negligible so failure scenarios run quickly.
            ["Yahoo:Resilience:RetryBaseDelay"] = "00:00:00.001",
        };

        foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
        {
            merged[key] = value;
        }

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(merged));
            builder.ConfigureLogging(logging => logging.AddProvider(LogProvider));
            builder.ConfigureServices(services =>
            {
                services.AddHttpClient<YahooFinanceClient>().ConfigurePrimaryHttpMessageHandler(() => yahoo);
                services.AddTransient<IStartupFilter, SimulatedClientAddressStartupFilter>();
                configureServices?.Invoke(services);

                if (timeProvider is not null)
                {
                    services.AddSingleton(timeProvider);
                }
            });
        });
    }

    /// <summary>Captures everything the application logs.</summary>
    public FakeLoggerProvider LogProvider { get; } = new();

    /// <summary>Starts the host (which validates configuration) and returns a client for it.</summary>
    public HttpClient CreateClient() => _factory.CreateClient();

    public void Dispose() => _factory.Dispose();

    public static IReadOnlyDictionary<string, string?> With(params (string Key, string Value)[] settings) =>
        settings.ToDictionary(s => s.Key, s => (string?)s.Value);

    private sealed class SimulatedClientAddressStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, continuation) =>
            {
                if (context.Request.Headers.TryGetValue(ClientAddressHeader, out var value) && IPAddress.TryParse(value, out var address))
                {
                    context.Connection.RemoteIpAddress = address;
                }

                return continuation();
            });

            next(app);
        };
    }
}
