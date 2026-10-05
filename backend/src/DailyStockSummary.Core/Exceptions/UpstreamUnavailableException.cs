namespace DailyStockSummary.Core.Exceptions;

public enum UpstreamFailureKind
{
    /// <summary>The provider failed, refused, or returned something we cannot interpret (HTTP 502).</summary>
    Unavailable,

    /// <summary>The provider did not answer in time (HTTP 504).</summary>
    Timeout,
}

/// <summary>The market data provider failed, timed out, or returned something we cannot interpret.</summary>
public sealed class UpstreamUnavailableException : Exception
{
    public UpstreamUnavailableException(string message, Exception? innerException = null)
        : this(message, UpstreamFailureKind.Unavailable, innerException)
    {
    }

    public UpstreamUnavailableException(string message, UpstreamFailureKind kind, Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
    }

    public UpstreamFailureKind Kind { get; }
}
