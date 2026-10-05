using System.Net;
using DailyStockSummary.Core.Abstractions;
using DailyStockSummary.Core.Services;
using DailyStockSummary.Infrastructure.Yahoo;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Timeout;

namespace DailyStockSummary.Infrastructure;

public static class DependencyInjection
{
    private const int MaxResponseBytes = 5 * 1024 * 1024;

    /// <summary>Registers the stock summary pipeline. Returns the Yahoo HttpClient builder so hosts and tests can adjust the transport.</summary>
    public static IHttpClientBuilder AddStockSummary(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<YahooOptions>()
            .Bind(configuration.GetSection(YahooOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<YahooOptions>, YahooOptionsValidator>();

        services.AddSingleton<IDailySummaryCalculator, DailySummaryCalculator>();
        services.AddTransient<IStockSummaryService, StockSummaryService>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IMarketDataProvider>(sp => new CachingMarketDataProvider(
            () => sp.GetRequiredService<YahooFinanceClient>(),
            sp.GetRequiredService<IOptions<YahooOptions>>().Value.CacheTtl,
            sp.GetRequiredService<TimeProvider>()));

        var builder = services.AddHttpClient<YahooFinanceClient>(client =>
        {
            // The resilience pipeline owns all timeouts; a response is a few hundred KB at most.
            client.Timeout = Timeout.InfiniteTimeSpan;
            client.MaxResponseContentBufferSize = MaxResponseBytes;
        });

        builder.AddResilienceHandler("yahoo", (pipeline, context) =>
        {
            var settings = context.ServiceProvider.GetRequiredService<IOptions<YahooOptions>>().Value.Resilience;

            // Outermost first: total budget, retries, circuit breaker, then the per-attempt timeout.
            pipeline.AddTimeout(new HttpTimeoutStrategyOptions { Timeout = settings.TotalTimeout });

            // Polly rejects zero retries, so "no retries" means leaving the strategy out.
            if (settings.RetryCount > 0)
            {
                pipeline.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = settings.RetryCount,
                    Delay = settings.RetryBaseDelay,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    ShouldHandle = args => ValueTask.FromResult(IsTransientFailure(args.Outcome)),
                });
            }

            pipeline
                .AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    MinimumThroughput = settings.BreakerMinimumThroughput,
                    FailureRatio = settings.BreakerFailureRatio,
                    SamplingDuration = settings.BreakerSamplingDuration,
                    BreakDuration = settings.BreakerBreakDuration,
                    ShouldHandle = args => ValueTask.FromResult(IsTransientFailure(args.Outcome)),
                })
                .AddTimeout(new HttpTimeoutStrategyOptions { Timeout = settings.AttemptTimeout });
        });

        return builder;
    }

    // Retry and trip the breaker on server errors, dropped connections and timeouts. Not on 404 (unknown
    // symbol) or 429: retrying a rate limit only deepens the block.
    private static bool IsTransientFailure(Outcome<HttpResponseMessage> outcome) =>
        outcome.Exception is HttpRequestException or TimeoutRejectedException
        || outcome.Result is { StatusCode: var status } && ((int)status >= 500 || status == HttpStatusCode.RequestTimeout);
}
