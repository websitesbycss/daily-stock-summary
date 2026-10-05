using Microsoft.AspNetCore.Mvc;

namespace DailyStockSummary.Api.Errors;

/// <summary>Builds the RFC 9457 problem bodies the API returns. The <c>type</c> URNs are a stable contract for clients.</summary>
public static class Problems
{
    private const string TypePrefix = "urn:daily-stock-summary:problem:";

    public static ProblemDetails InvalidSymbol(string detail) =>
        Create(StatusCodes.Status400BadRequest, "invalid-symbol", "Invalid symbol", detail);

    public static ProblemDetails SymbolNotFound(string symbol) =>
        Create(StatusCodes.Status404NotFound, "symbol-not-found", "Symbol not found", $"No market data found for symbol '{symbol}'.");

    public static ProblemDetails RateLimited() =>
        Create(StatusCodes.Status429TooManyRequests, "rate-limited", "Too many requests", "Too many requests. Try again shortly.");

    public static ProblemDetails UpstreamUnavailable() =>
        Create(
            StatusCodes.Status502BadGateway,
            "upstream-unavailable",
            "Market data provider unavailable",
            "The market data provider is unavailable. Try again shortly.");

    public static ProblemDetails UpstreamTimeout() =>
        Create(
            StatusCodes.Status504GatewayTimeout,
            "upstream-timeout",
            "Market data provider timed out",
            "The market data provider did not respond in time. Try again shortly.");

    public static ProblemDetails Unexpected() =>
        Create(StatusCodes.Status500InternalServerError, "unexpected-error", "An unexpected error occurred", "The request could not be completed.");

    private static ProblemDetails Create(int status, string slug, string title, string detail) =>
        new() { Status = status, Type = TypePrefix + slug, Title = title, Detail = detail };
}
