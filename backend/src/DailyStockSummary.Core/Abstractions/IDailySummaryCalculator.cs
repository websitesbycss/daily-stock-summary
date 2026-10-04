using DailyStockSummary.Core.Models;

namespace DailyStockSummary.Core.Abstractions;

public interface IDailySummaryCalculator
{
    /// <summary>Groups bars by exchange-local day and returns the summaries ordered oldest first.</summary>
    IReadOnlyList<DailySummary> Calculate(IntradaySeries series);
}
