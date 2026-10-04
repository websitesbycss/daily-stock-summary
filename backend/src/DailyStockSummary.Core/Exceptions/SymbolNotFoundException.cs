using DailyStockSummary.Core.Models;

namespace DailyStockSummary.Core.Exceptions;

public sealed class SymbolNotFoundException : Exception
{
    public SymbolNotFoundException(Symbol symbol) : base($"No market data found for symbol '{symbol}'.")
    {
        Symbol = symbol;
    }

    public Symbol Symbol { get; }
}
