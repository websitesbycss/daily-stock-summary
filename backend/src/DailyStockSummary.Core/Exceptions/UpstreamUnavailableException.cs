namespace DailyStockSummary.Core.Exceptions;

/// <summary>The market data provider failed, timed out, or returned something we cannot interpret.</summary>
public sealed class UpstreamUnavailableException : Exception
{
    public UpstreamUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
