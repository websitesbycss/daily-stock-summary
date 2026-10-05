using System.Net;
using DailyStockSummary.Core.Abstractions;
using DailyStockSummary.Core.Exceptions;
using DailyStockSummary.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace DailyStockSummary.Infrastructure.Yahoo;

public sealed partial class YahooFinanceClient(HttpClient httpClient, IOptions<YahooOptions> options, ILogger<YahooFinanceClient> logger) : IMarketDataProvider
{
    public async Task<IntradaySeries> GetIntradayAsync(Symbol symbol, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildUri(symbol));
        request.Headers.UserAgent.ParseAdd(options.Value.UserAgent);

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                // Only Yahoo's own "Not Found" chart error means the symbol is unknown. A proxy page or a
                // wrong base URL also answers 404 and must surface as an upstream failure instead.
                ThrowIfSymbolNotFound(symbol, await response.Content.ReadAsStringAsync(cancellationToken), logger);
            }

            if (!response.IsSuccessStatusCode)
            {
                LogUnexpectedStatus(logger, symbol.Value, (int)response.StatusCode);
                throw new UpstreamUnavailableException($"Yahoo responded with HTTP {(int)response.StatusCode}.");
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return YahooChartParser.Parse(symbol, body, logger);
        }
        catch (HttpRequestException ex)
        {
            throw new UpstreamUnavailableException("Could not reach Yahoo.", ex);
        }
        catch (BrokenCircuitException ex)
        {
            throw new UpstreamUnavailableException("Yahoo is failing repeatedly; requests are paused.", ex);
        }
        catch (TimeoutRejectedException ex)
        {
            throw new UpstreamUnavailableException("The request to Yahoo timed out.", UpstreamFailureKind.Timeout, ex);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient surfaces its own timeout as a cancellation the caller did not request.
            throw new UpstreamUnavailableException("The request to Yahoo timed out.", UpstreamFailureKind.Timeout, ex);
        }
    }

    private static void ThrowIfSymbolNotFound(Symbol symbol, string body, ILogger logger)
    {
        try
        {
            YahooChartParser.Parse(symbol, body, logger);
        }
        catch (UpstreamUnavailableException)
        {
            // Not a Yahoo chart payload: the caller reports the plain HTTP 404 as an upstream failure.
        }
    }

    private Uri BuildUri(Symbol symbol)
    {
        var settings = options.Value;
        var path = $"v8/finance/chart/{Uri.EscapeDataString(symbol.Value)}";
        var query = $"interval={Uri.EscapeDataString(settings.Interval)}&range={Uri.EscapeDataString(settings.Range)}";

        return new Uri(settings.BaseUrl, $"{path}?{query}");
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Yahoo answered {StatusCode} for {Symbol}.")]
    private static partial void LogUnexpectedStatus(ILogger logger, string symbol, int statusCode);
}
