using DailyStockSummary.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DailyStockSummary.Tests.TestSupport;

/// <summary>Builds the real stock summary stack (options, resilience, cache, client) with only the network replaced.</summary>
public static class StockSummaryTestHost
{
    public static ServiceProvider Build(HttpMessageHandler handler, IReadOnlyDictionary<string, string?>? settings = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings ?? new Dictionary<string, string?>())
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddStockSummary(configuration).ConfigurePrimaryHttpMessageHandler(() => handler);

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }
}
