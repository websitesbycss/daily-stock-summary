using DailyStockSummary.Core.Models;

namespace DailyStockSummary.Core.Abstractions;

public interface IMarketDataProvider
{
    /// <summary>Fetches the last month of 15-minute bars for the symbol.</summary>
    /// <exception cref="Exceptions.SymbolNotFoundException">The provider has no data for the symbol.</exception>
    /// <exception cref="Exceptions.UpstreamUnavailableException">The provider failed or returned unusable data.</exception>
    Task<IntradaySeries> GetIntradayAsync(Symbol symbol, CancellationToken cancellationToken);
}
