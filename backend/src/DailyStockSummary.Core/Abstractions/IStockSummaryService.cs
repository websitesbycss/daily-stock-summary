using DailyStockSummary.Core.Models;

namespace DailyStockSummary.Core.Abstractions;

public interface IStockSummaryService
{
    /// <summary>Returns one summary per trading day in the provider's window, oldest first.</summary>
    Task<IReadOnlyList<DailySummary>> GetDailySummariesAsync(Symbol symbol, CancellationToken cancellationToken);
}
