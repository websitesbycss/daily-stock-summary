namespace DailyStockSummary.Core.Exceptions;

public sealed class InvalidSymbolException : Exception
{
    public InvalidSymbolException(string message) : base(message)
    {
    }
}
