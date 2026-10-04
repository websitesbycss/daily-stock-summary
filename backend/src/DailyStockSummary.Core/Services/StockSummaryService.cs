using DailyStockSummary.Core.Abstractions;
using DailyStockSummary.Core.Models;

namespace DailyStockSummary.Core.Services;

public sealed class StockSummaryService(IMarketDataProvider provider, IDailySummaryCalculator calculator) : IStockSummaryService
{
    public async Task<IReadOnlyList<DailySummary>> GetDailySummariesAsync(Symbol symbol, CancellationToken cancellationToken)
    {
        var series = await provider.GetIntradayAsync(symbol, cancellationToken);
        return calculator.Calculate(series);
    }
}
